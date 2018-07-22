using System;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation;

namespace RunnerRemoteExecutionService
{
    public sealed class RemoteExecutor : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private AppServiceConnection appServiceconnection;
        internal const int SuccessExitCode = 42;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral(); // Get a deferral so that the service isn't terminated.
            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // Get a deferral because we use an awaitable API below to respond to the message
            // and we don't want this call to get cancelled while we are waiting.
            var messageDeferral = args.GetDeferral();

            ValueSet returnData = new ValueSet();
            HandleTheRequest(args.Request.Message, returnData);

            // Return the data to the caller.
            // Complete the deferral so that the platform knows that we're done responding to the app service call.
            // Note for error handling: this must be called even if SendResponseAsync() throws an exception.
            await args.Request.SendResponseAsync(returnData); 

            messageDeferral.Complete();
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();
            }
        }

        private void HandleTheRequest(ValueSet message, ValueSet returnData)
        {
            string assemblyName;
            string typeName;
            string methodName;
            string[] additionalArgs;

            // default the results to SuccessExitCode as success. We override it at failures.
            returnData["Results"] = SuccessExitCode;

            // The message expects to be passed the target assembly name to load, the type
            // from that assembly to find, and the method from that assembly to invoke.
            // Any additional arguments are passed as strings to the method.

            if (!GetStringValueFromValueSet(message, "AssemblyName", out assemblyName) || String.IsNullOrEmpty(assemblyName))
            {
                returnData["Results"] = -1;
                returnData["Log"] = $"RemoteExecuter Error: Missing assembly name";
                return;
            }

            if (!GetStringValueFromValueSet(message, "TypeName", out typeName) || String.IsNullOrEmpty(typeName))
            {
                returnData["Results"] = -2;
                returnData["Log"] = $"RemoteExecuter Error: Missing type name";
                return;
            }

            if (!GetStringValueFromValueSet(message, "MethodName", out methodName) || String.IsNullOrEmpty(methodName))
            {
                returnData["Results"] = -3;
                returnData["Log"] = $"RemoteExecuter Error: Missing method name";
                return;
            }

            List<string> argList = new List<string>();
            int i = 0;
            string arg;
            while (GetStringValueFromValueSet(message, "Arg"+i, out arg))
            {
                argList.Add(arg);
                i++;
            }
            additionalArgs = argList.ToArray();

            StringBuilder log = new StringBuilder();

            // Load the specified assembly, type, and method, then invoke the method.
            // The program's exit code is the return value of the invoked method.
            object instance = null;
            log.Append($"RemoteExecuter: {assemblyName} {methodName} {string.Join(", ", additionalArgs)}{Environment.NewLine}");

            try
            {
                // Create the test class if necessary
                Assembly a = Assembly.Load(new AssemblyName(assemblyName));
                Type t = a.GetType(typeName);
                MethodInfo mi = t.GetTypeInfo().GetDeclaredMethod(methodName);
                if (!mi.IsStatic)
                {
                    instance = Activator.CreateInstance(t);
                }

                // Invoke the test
                object result = mi.Invoke(instance, additionalArgs);

                int exitCode = 0;
                if (result is Task<int> task)
                {
                    exitCode = task.GetAwaiter().GetResult();
                }
                else if (result is int exit)
                {
                    exitCode = exit;
                }

                returnData["Results"] = exitCode;
            }
            catch (Exception exc)
            {
                returnData["Results"] = -4;
                log.Append(exc);
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }
            returnData["Log"] = log.ToString();
        }

        private bool GetStringValueFromValueSet(ValueSet message, string key, out string val)
        {
            val = null;
            
            object valObject;
            if (!message.TryGetValue(key, out valObject))
                return false;
            
            val = (string)valObject;

            return true;
        }
    }
}
