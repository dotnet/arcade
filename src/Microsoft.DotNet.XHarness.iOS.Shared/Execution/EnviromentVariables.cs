namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution;

public static class EnviromentVariables
{

    /// <summary>
    /// Env var that will tell the runner to start the execution of the tests automatically.
    /// </summary>
    public const string AutoStart = "NUNIT_AUTOSTART";

    /// <summary>
    /// Env var that will tell the test application to exit once all the test have been ran.
    /// </summary>
    public const string AutoExit = "NUNIT_AUTOEXIT";

    /// <summary>
    /// Env var that will tell the test application to enable network on the device (iOS).
    /// </summary>
    public const string EnableNetwork = "NUNIT_ENABLE_NETWORK";

    /// <summary>
    /// Env var that will tell the test application to ignore those tests that required a permission to
    /// execute on the device.
    /// </summary>
    public const string DisableSystemPermissionTests = "DISABLE_SYSTEM_PERMISSION_TESTS";

    /// <summary>
    /// Env var that provide the test application the name of the host.
    /// </summary>
    public const string HostName = "NUNIT_HOSTNAME";

    /// <summary>
    /// Env var that provides the test application with the transport to use to communicate with the host.
    /// </summary>
    public const string Transport = "NUNIT_TRANSPORT";

    /// <summary>
    /// Env var that provides the test application with the path to be used to store the execution logs.
    /// </summary>
    public const string LogFilePath = "NUNIT_LOG_FILE";

    /// <summary>
    /// Env var that provide the test application the port to be used to connect with the host.
    /// </summary>
    public const string HostPort = "NUNIT_HOSTPORT";

    /// <summary>
    /// Env var used to notify the test application that the communication will be done using a tcp tunnel
    /// over the usb cable.
    /// </summary>
    public const string UseTcpTunnel = "USE_TCP_TUNNEL";

    /// <summary>
    /// Env var containing a tag that the test application will output once tests are finished to signalize it.
    /// </summary>
    public const string AppEndTag = "RUN_END_TAG";

    /// <summary>
    /// Env var used to notify the test application that the output is expected to be xml.
    /// </summary>
    public const string EnableXmlOutput = "NUNIT_ENABLE_XML_OUTPUT";

    /// <summary>
    /// Env var used to notify the test application the xml mode to be used.
    /// </summary>
    public const string XmlMode = "NUNIT_ENABLE_XML_MODE";

    /// <summary>
    /// Env var used to pass the format of the xml used for results.
    /// </summary>
    public const string XmlVersion = "NUNIT_XML_VERSION";

    /// <summary>
    /// Env var used to notify the test application that the test should be sorted by name.
    /// </summary>
    public const string SortByName = "NUNIT_SORTNAMES";

    /// <summary>
    /// Env var used to notify the test application if all the tests should be ran by default.
    /// </summary>
    public const string RunAllTestsByDefault = "NUNIT_RUN_ALL";

    /// <summary>
    /// Env var used to notify the test application which tests will be excluded.
    /// </summary>
    public const string SkippedMethods = "NUNIT_SKIPPED_METHODS";

    /// <summary>
    /// Env var uses to notify the test application which test classes will be excluded.
    /// </summary>
    public const string SkippedClasses = "NUNIT_SKIPPED_CLASSES";
}
