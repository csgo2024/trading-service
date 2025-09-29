namespace Trading.Common.Models;

public class ApiProxySettings
{
    public const string Section = "ApiProxySettings";
    /// <summary>
    /// The host address of the proxy
    /// </summary>
    public string Host { get; set; } = "";
    /// <summary>
    /// The port of the proxy
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The login of the proxy
    /// </summary>
    public string? Login { get; set; }

    /// <summary>
    /// The password of the proxy
    /// </summary>
    public string? Password { get; set; }
}
