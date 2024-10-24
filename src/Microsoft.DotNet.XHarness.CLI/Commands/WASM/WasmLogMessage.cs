namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

class WasmLogMessage
{
    public string? method { get; set; }
    public string? payload { get; set; }
    public object[]? arguments { get; set; }
}
