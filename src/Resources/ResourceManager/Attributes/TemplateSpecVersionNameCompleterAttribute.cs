// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.Azure.Commands.ResourceManager.Cmdlets.Attributes
{
    using Microsoft.Azure.Commands.Common.Authentication;
    using Microsoft.Azure.Commands.Common.Authentication.Abstractions;
    using Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters;
    using Microsoft.Azure.Management.ResourceManager;
    using System;
    using System.Linq;
    using System.Management.Automation;

    /// <summary>
    /// An argument completer for completing template spec version names. If the list
    /// of current arguments contains the -BuiltIn flag it'll do completion based
    /// on available built-in template spec version names (tenant level resources). If 
    /// the flag is not present the standard ResourceNameCompleterAttribute logic will be
    /// wrapped/leveraged.
    /// </summary>
    public class TemplateSpecVersionNameCompleterAttribute : ArgumentCompleterAttribute
    {
        /// <summary>
        /// The max time allowed for built-ins to be returned before the completer
        /// will just gracefully return an empty result. 
        /// </summary>
        private static TimeSpan _builtInsTimeout = TimeSpan.FromSeconds(3);

        public TemplateSpecVersionNameCompleterAttribute(
            string builtInFlagName,
            string resourceGroupParameterName,
            string templateSpecNameParameterName) : base(
                CreateScriptBlock(
                    builtInFlagName,
                    resourceGroupParameterName,
                    templateSpecNameParameterName
                )
            )
        {
        }

        /// <summary>
        /// Creates ScriptBlock that will use the standard ResourceNameCompleter if the
        /// completion context doesn't include the built-ins flag, but will otherwise return
        /// the appropriate completion result based on built-in template spec version names.
        /// </summary>
        /// <remarks>
        /// We use ScriptBlock here instead of implementing IArgumentCompleter because we use
        /// ResourceNameCompleterAttribute under the hood for non-built-ins.
        /// </remarks>
        public static ScriptBlock CreateScriptBlock(
            string builtInFlagName,
            string resourceGroupParameterName,
            string templateSpecNameParameterName)
        {
            // Create the standard resource name argument completer we'll use when the
            // -BuiltIn flag isn't provided:

            var standardResourceNameCompleter = new ResourceNameCompleterAttribute(
                "Microsoft.Resources/templateSpecs/versions",
                resourceGroupParameterName,
                templateSpecNameParameterName
            );

            string script = "param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)\n" +
                $"$completerScriptBlock = {{ {standardResourceNameCompleter.ScriptBlock} }}\n" +
                "if (-Not $fakeBoundParameters.ContainsKey('" + builtInFlagName + "')) {\n" +
                "& $completerScriptBlock $commandName $parameterName $wordToComplete $commandAst $fakeBoundParameters\n" +
                "} else {\n" +
                "$builtInVersionNames = [Microsoft.Azure.Commands.ResourceManager.Cmdlets.Attributes.TemplateSpecVersionNameCompleterAttribute]::GetAvailableBuiltInVersionNames($fakeBoundParameters['" + templateSpecNameParameterName + "'])\n" +
                "$builtInVersionNames | Where-Object { $_ -Like \"$wordToComplete*\" } | Sort-Object | ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }\n" +
                "}";

            ScriptBlock scriptBlock = ScriptBlock.Create(script);
            return scriptBlock;
        }

        public static string[] GetAvailableBuiltInVersionNames(string builtInTemplateSpecName)
        {
            IAzureContext context = AzureRmProfileProvider.Instance?.Profile?.DefaultContext;
            var client = AzureSession.Instance.ClientFactory.CreateArmClient<TemplateSpecsClient>(context,
                    AzureEnvironment.Endpoint.ResourceManager);

            var builtInVersions = client.TemplateSpecVersions.ListBuiltInsAsync(builtInTemplateSpecName);

            bool hasTimedOut = false;
            try
            {
                hasTimedOut = !builtInVersions.Wait(_builtInsTimeout);
            }
            catch (Exception)
            {
                // Eat the exception
            }

#if DEBUG
            if (hasTimedOut)
            {
                throw new TimeoutException("Getting built-in template spec timed out");
            }
#endif

            var hasResult = !hasTimedOut && builtInVersions.Result != null;
            return hasResult
                ? builtInVersions.Result.Select(builtInVersion => builtInVersion.Name).ToArray()
                : new string[0];
        }
    }
}
