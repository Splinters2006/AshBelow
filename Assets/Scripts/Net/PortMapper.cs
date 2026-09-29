using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Slopgame
{
    public enum PortMapMethod { None, Upnp, NatPmp }

    /// <summary>What <see cref="PortMapper.OpenAsync"/> managed to do; any field may be empty.</summary>
    public sealed class PortMapResult
    {
        public PortMapMethod Method;
        public ushort ExternalPort;
        /// <summary>The address friends on the internet should join, when it is known.</summary>
        public IPAddress ExternalAddress;
        public IPAddress LocalAddress;
        public string Problem;
        public bool Opened => Method != PortMapMethod.None;
    }

    /// <summary>
    /// Opens a UDP port on the host's home router so friends can connect peer-to-peer without manual port
    /// forwarding. Tries UPnP (most routers) first, then NAT-PMP (Apple and some others). Everything here runs
    /// off the main thread and never touches Unity APIs, so it is safe to wait on briefly while quitting.
    /// </summary>
    public sealed class PortMapper
    {
        private const string Description = "Ash Below co-op";
        private const int LeaseSeconds = 7200;
        private static readonly TimeSpan Renewal = TimeSpan.FromMinutes(50);
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

        private readonly CancellationTokenSource renew = new CancellationTokenSource();
        private PortMapResult result;
        private string controlUrl, serviceType;
        private IPAddress gateway;
        private int lease;

        public async Task<PortMapResult> OpenAsync(ushort port)
        {
            result = new PortMapResult { LocalAddress = LocalAddress() };
            if (result.LocalAddress == null) { result.Problem = "This PC is not connected to a network."; return result; }
            try
            {
                if (await TryUpnp(port).ConfigureAwait(false)) result.Method = PortMapMethod.Upnp;
                else if (await TryNatPmp(port).ConfigureAwait(false)) result.Method = PortMapMethod.NatPmp;
            }
            catch (Exception error) { result.Problem = error.Message; }
            if (!result.Opened) result.Problem = result.Problem ?? "Your router did not allow the game to open a port (UPnP / NAT-PMP is off).";
            else _ = RenewLoop(port);
            return result;
        }

        /// <summary>Removes the router mapping. Safe to call more than once or when nothing was opened.</summary>
        public async Task CloseAsync()
        {
            renew.Cancel();
            var opened = result;
            result = null;
            if (opened == null || !opened.Opened) return;
            try
            {
                if (opened.Method == PortMapMethod.Upnp)
                    await Soap("DeletePortMapping", $"<NewRemoteHost></NewRemoteHost><NewExternalPort>{opened.ExternalPort}</NewExternalPort><NewProtocol>UDP</NewProtocol>").ConfigureAwait(false);
                else await NatPmpMap(opened.ExternalPort, 0, 0).ConfigureAwait(false);
            }
            catch { /* The lease expires on its own. */ }
        }

        private async Task RenewLoop(ushort port)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(Renewal, renew.Token).ConfigureAwait(false);
                    if (result == null) return;
                    if (result.Method == PortMapMethod.Upnp) await AddUpnpMapping(port, lease).ConfigureAwait(false);
                    else await NatPmpMap(port, port, LeaseSeconds).ConfigureAwait(false);
                }
            }
            catch { /* Cancelled, or the router went away; the game keeps running either way. */ }
        }

        // ---------------------------------------------------------------- UPnP

        private async Task<bool> TryUpnp(ushort port)
        {
            foreach (string location in await DiscoverGateways(result.LocalAddress).ConfigureAwait(false))
            {
                if (!await ReadDescription(location).ConfigureAwait(false)) continue;
                string added = await AddUpnpMapping(port, LeaseSeconds).ConfigureAwait(false);
                // Some older routers only accept permanent mappings (error 725).
                if (added != null && added.Contains("725")) added = await AddUpnpMapping(port, 0).ConfigureAwait(false);
                if (added != null) { result.Problem = "Your router refused to open the port: " + added; continue; }
                result.ExternalPort = port;
                string reply = await Soap("GetExternalIPAddress", "").ConfigureAwait(false);
                if (IPAddress.TryParse(Tag(reply, "NewExternalIPAddress") ?? "", out var external)) result.ExternalAddress = external;
                result.Problem = null;
                return true;
            }
            return false;
        }

        /// <summary>Returns null on success, or the router's error.</summary>
        private async Task<string> AddUpnpMapping(ushort port, int leaseSeconds)
        {
            lease = leaseSeconds;
            try
            {
                await Soap("AddPortMapping", "<NewRemoteHost></NewRemoteHost>"
                    + $"<NewExternalPort>{port}</NewExternalPort><NewProtocol>UDP</NewProtocol><NewInternalPort>{port}</NewInternalPort>"
                    + $"<NewInternalClient>{result.LocalAddress}</NewInternalClient><NewEnabled>1</NewEnabled>"
                    + $"<NewPortMappingDescription>{Description}</NewPortMappingDescription><NewLeaseDuration>{leaseSeconds}</NewLeaseDuration>").ConfigureAwait(false);
                return null;
            }
            catch (Exception error) { return error.Message; }
        }

        private static async Task<List<string>> DiscoverGateways(IPAddress local)
        {
            var locations = new List<string>();
            string[] targets =
            {
                "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
                "urn:schemas-upnp-org:service:WANIPConnection:1", "urn:schemas-upnp-org:service:WANPPPConnection:1"
            };
            using var udp = new UdpClient(new IPEndPoint(local, 0));
            var multicast = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            foreach (string target in targets)
            {
                byte[] search = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\n"
                    + $"MAN: \"ssdp:discover\"\r\nMX: 2\r\nST: {target}\r\n\r\n");
                await udp.SendAsync(search, search.Length, multicast).ConfigureAwait(false);
            }
            var deadline = DateTime.UtcNow.AddSeconds(2.5);
            while (DateTime.UtcNow < deadline)
            {
                var receive = udp.ReceiveAsync();
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || await Task.WhenAny(receive, Task.Delay(remaining)).ConfigureAwait(false) != receive) { Observe(receive); break; }
                string location = SsdpLocation(Encoding.ASCII.GetString(receive.Result.Buffer));
                if (location != null && !locations.Contains(location)) locations.Add(location);
            }
            return locations;
        }

        /// <summary>A receive abandoned on timeout fails when its socket closes; swallow that quietly.</summary>
        private static void Observe(Task task) => task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

        /// <summary>The LOCATION header of an SSDP reply, or null.</summary>
        public static string SsdpLocation(string reply)
        {
            foreach (string line in reply.Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon > 0 && line.Substring(0, colon).Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                    return line.Substring(colon + 1).Trim();
            }
            return null;
        }

        private async Task<bool> ReadDescription(string location)
        {
            try
            {
                var xml = XDocument.Parse(await Http.GetStringAsync(location).ConfigureAwait(false));
                string urlBase = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "URLBase")?.Value;
                foreach (var service in xml.Descendants().Where(e => e.Name.LocalName == "service"))
                {
                    string type = Child(service, "serviceType");
                    string control = Child(service, "controlURL");
                    if (type == null || control == null || !(type.Contains("WANIPConnection") || type.Contains("WANPPPConnection"))) continue;
                    serviceType = type.Trim();
                    controlUrl = new Uri(new Uri(string.IsNullOrWhiteSpace(urlBase) ? location : urlBase), control.Trim()).ToString();
                    return true;
                }
            }
            catch { /* Not a usable gateway; try the next one. */ }
            return false;
        }

        private static string Child(XElement parent, string name) => parent.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        private async Task<string> Soap(string action, string arguments)
        {
            string body = "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body>"
                + $"<u:{action} xmlns:u=\"{serviceType}\">{arguments}</u:{action}></s:Body></s:Envelope>";
            using var request = new HttpRequestMessage(HttpMethod.Post, controlUrl) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{serviceType}#{action}\"");
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            string reply = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"{Tag(reply, "errorCode") ?? ((int)response.StatusCode).ToString()} {Tag(reply, "errorDescription")}".Trim());
            return reply;
        }

        private static string Tag(string xml, string name)
        {
            int start = xml.IndexOf("<" + name + ">", StringComparison.Ordinal);
            if (start < 0) return null;
            start += name.Length + 2;
            int end = xml.IndexOf("</" + name + ">", start, StringComparison.Ordinal);
            return end < 0 ? null : xml.Substring(start, end - start).Trim();
        }

        // ---------------------------------------------------------------- NAT-PMP

        private async Task<bool> TryNatPmp(ushort port)
        {
            gateway = Gateway(result.LocalAddress);
            if (gateway == null) return false;
            byte[] mapped = await NatPmpMap(port, port, LeaseSeconds).ConfigureAwait(false);
            if (mapped == null || mapped.Length < 16 || mapped[1] != 129 || mapped[2] != 0 || mapped[3] != 0) return false;
            result.ExternalPort = (ushort)(mapped[10] << 8 | mapped[11]);
            byte[] address = await NatPmpRequest(new byte[] { 0, 0 }).ConfigureAwait(false);
            if (address != null && address.Length >= 12 && address[1] == 128 && address[3] == 0)
                result.ExternalAddress = new IPAddress(new[] { address[8], address[9], address[10], address[11] });
            return true;
        }

        private Task<byte[]> NatPmpMap(ushort internalPort, ushort externalPort, int lifetime) => NatPmpRequest(new byte[]
        {
            0, 1, 0, 0, (byte)(internalPort >> 8), (byte)internalPort, (byte)(externalPort >> 8), (byte)externalPort,
            (byte)(lifetime >> 24), (byte)(lifetime >> 16), (byte)(lifetime >> 8), (byte)lifetime
        });

        private async Task<byte[]> NatPmpRequest(byte[] request)
        {
            if (gateway == null) return null;
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            var target = new IPEndPoint(gateway, 5351);
            for (int attempt = 0, wait = 250; attempt < 3; attempt++, wait *= 2)
            {
                await udp.SendAsync(request, request.Length, target).ConfigureAwait(false);
                var receive = udp.ReceiveAsync();
                if (await Task.WhenAny(receive, Task.Delay(wait)).ConfigureAwait(false) == receive) return receive.Result.Buffer;
                Observe(receive);
            }
            return null;
        }

        private static IPAddress Gateway(IPAddress local)
        {
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var properties = adapter.GetIPProperties();
                    if (!properties.UnicastAddresses.Any(a => a.Address.Equals(local))) continue;
                    var found = properties.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
                    if (found != null) return found;
                }
            }
            catch { /* Some platforms cannot list gateways. */ }
            // Home routers almost always sit at .1 of the local /24.
            byte[] bytes = local.GetAddressBytes();
            bytes[3] = 1;
            return new IPAddress(bytes);
        }

        // ---------------------------------------------------------------- addresses

        /// <summary>The LAN address this PC uses to reach the internet, or null when offline.</summary>
        public static IPAddress LocalAddress()
        {
            try
            {
                // Connecting a UDP socket sends nothing; it only asks the OS which interface would be used.
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 53);
                return ((IPEndPoint)socket.LocalEndPoint).Address;
            }
            catch { return null; }
        }

        /// <summary>True for addresses nobody on the internet can reach (LAN, carrier-grade NAT, loopback).</summary>
        public static bool IsPrivate(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork) return true;
            byte[] b = address.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        /// <summary>Parses "host", "host:port" or "[ipv6]:port" as typed or pasted by a player.</summary>
        public static bool TryParseEndpoint(string text, ushort defaultPort, out string host, out ushort port)
        {
            host = (text ?? "").Trim();
            port = defaultPort;
            if (host.Length == 0) return false;
            if (host.StartsWith("["))
            {
                int close = host.IndexOf(']');
                if (close < 0) return false;
                string rest = host.Substring(close + 1);
                host = host.Substring(1, close - 1);
                return rest.Length == 0 || (rest.StartsWith(":") && ushort.TryParse(rest.Substring(1), out port) && port > 0);
            }
            int colon = host.LastIndexOf(':');
            // More than one colon without brackets is a bare IPv6 address.
            if (colon > 0 && host.IndexOf(':') == colon)
            {
                if (!ushort.TryParse(host.Substring(colon + 1), out port) || port == 0) return false;
                host = host.Substring(0, colon);
            }
            return host.Length > 0 && host.IndexOf(' ') < 0;
        }
    }
}
