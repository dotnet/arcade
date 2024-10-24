// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

public static class Extensions
{
    public static string AsString(this TestTarget target) => target switch
    {
        TestTarget.Device_iOS => "ios-device",
        TestTarget.Device_tvOS => "tvos-device",
        TestTarget.Device_watchOS => "watchos-device",
        TestTarget.Device_xrOS => "xros-device",
        TestTarget.Simulator_iOS64 => "ios-simulator-64",
        TestTarget.Simulator_tvOS => "tvos-simulator",
        TestTarget.Simulator_watchOS => "watchos-simulator",
        TestTarget.Simulator_xrOS => "xros-simulator",
        TestTarget.MacCatalyst => "maccatalyst",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    public static string AsString(this TestTargetOs targetOs) =>
        targetOs.Platform.AsString() + (targetOs.OSVersion != null ? "_" + targetOs.OSVersion : null);

    public static TestTarget ParseAsAppRunnerTarget(this string target) => target switch
    {
        "ios-device" => TestTarget.Device_iOS,
        "tvos-device" => TestTarget.Device_tvOS,
        "watchos-device" => TestTarget.Device_watchOS,
        "xros-device" => TestTarget.Device_xrOS,
        "ios-simulator" => TestTarget.Simulator_iOS64,
        "ios-simulator-64" => TestTarget.Simulator_iOS64,
        "tvos-simulator" => TestTarget.Simulator_tvOS,
        "watchos-simulator" => TestTarget.Simulator_watchOS,
        "xros-simulator" => TestTarget.Simulator_xrOS,
        "maccatalyst" => TestTarget.MacCatalyst,
        null => TestTarget.None,
        "" => TestTarget.None,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    public static TestTargetOs ParseAsAppRunnerTargetOs(this string targetName)
    {
        var index = targetName.LastIndexOf('_');
        TestTarget target;
        string? osVersion = null;
        if (index != -1)
        {
            target = targetName.Substring(0, index).ParseAsAppRunnerTarget();
            osVersion = targetName.Substring(index + 1);
        }
        else
        {
            target = targetName.ParseAsAppRunnerTarget();
        }

        return new TestTargetOs(target, osVersion);
    }

    public static Extension ParseFromNSExtensionPointIdentifier(this string identifier) => identifier switch
    {
        "com.apple.widget-extension" => Extension.TodayExtension,
        "com.apple.watchkit" => Extension.WatchKit2,
        _ => throw new ArgumentOutOfRangeException(nameof(identifier))
    };

    public static string AsNSExtensionPointIdentifier(this Extension extension) => extension switch
    {
        Extension.TodayExtension => "com.apple.widget-extension",
        Extension.WatchKit2 => "com.apple.watchkit",
        _ => throw new ArgumentOutOfRangeException(nameof(extension))
    };

    public static void DoNotAwait(this Task task)
    {
        // Don't do anything!
        //
        // Here's why:
        // If you want to run a task in the background, and you don't care about the result, this is the obvious way to do so:
        //
        //     DoSomethingAsync ();
        //
        // which works fine, but the compiler warns that:
        //
        //     Because this call is not awaited, execution of the current method continues before the call is completed. Consider applying the 'await' operator to the result of the call.
        //
        // One potential fix is to assign the return value to variable:
        //
        //     var x = DoSomethingAsync ();
        //
        // But this creates unnecessary variables. It's also still slightly confusing (why assign to a variable that's not used?).
        // This extension method allows us to be more explicit:
        //
        //     DoSomethingAsync ().DoNotAwait ();
        //
        // This makes it abundantly clear that the intention is to not await 'DoSomething', and no warnings will be shown either.
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection)
    {
        var rnd = new Random((int)DateTime.Now.Ticks);
        return collection.OrderBy(v => rnd.Next());
    }

    public static string AsHtml(this string inString)
    {
        var rv = System.Web.HttpUtility.HtmlEncode(inString);
        return rv.Replace("\t", "&nbsp;&nbsp;&nbsp;&nbsp;").Replace("\n", "<br/>\n");
    }

    // XmlWriter.WriteCData will throw for some characters. This method will catch that exception, and write a base64 encoded string instead (which should always be safe).
    public static void WriteCDataSafe(this XmlWriter writer, string text)
    {
        try
        {
            writer.WriteCData(text);
        }
        catch (ArgumentException)
        {
            var utf8 = Encoding.UTF8.GetBytes(text);
            var base64 = Convert.ToBase64String(utf8);
            writer.WriteCData("Base64 encoded UTF8 string: " + base64);
        }
    }
}
