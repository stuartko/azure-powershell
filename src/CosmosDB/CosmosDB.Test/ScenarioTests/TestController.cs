﻿// ----------------------------------------------------------------------------------
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Azure.Commands.Common.Authentication;
using Microsoft.Azure.Internal.Common;
using Microsoft.Azure.Management.Internal.Resources;
using Microsoft.Azure.Management.CosmosDB;
using Microsoft.Azure.Management.Network;
using Microsoft.Azure.Test.HttpRecorder;
using Microsoft.Rest.ClientRuntime.Azure.TestFramework;
using Microsoft.WindowsAzure.Commands.ScenarioTest;
using Microsoft.WindowsAzure.Commands.Test.Utilities.Common;
using TestEnvironmentFactory = Microsoft.Rest.ClientRuntime.Azure.TestFramework.TestEnvironmentFactory;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Commands.Common.KeyVault.Version2016_10_1;

namespace Microsoft.Azure.Commands.CosmosDB.Test.ScenarioTests.ScenarioTest
{
    public class TestController : RMTestBase
    {
        private readonly EnvironmentSetupHelper _helper;

        public ResourceManagementClient ResourceManagementClient { get; private set; }

        public CosmosDBManagementClient CosmosDBManagementClient { get; private set; }

        public NetworkManagementClient NetworkManagementClient { get; private set; }

        public AzureRestClient AzureRestClient { get; private set; }

        public KeyVaultManagementClient KeyVaultManagementClient { get; private set; }

        public KeyVaultClient KeyVaultClient { get; private set; }

        public static CosmosDBTestHelper CosmosDBTestHelper { get; private set; }

        public static TestController NewInstance => new TestController();

        protected TestController()
        {
            _helper = new EnvironmentSetupHelper();
        }

        protected void SetupManagementClients(MockContext context)
        {
            ResourceManagementClient = GetResourceManagementClient(context);
            CosmosDBManagementClient = GetCosmosDBManagementClient(context);
            NetworkManagementClient = GetNetworkManagementClient(context);
            AzureRestClient = GetAzureRestClient(context);
            KeyVaultManagementClient = GetKeyVaultManagementClient(context);
            _helper.SetupManagementClients(
                ResourceManagementClient,
                CosmosDBManagementClient,
                NetworkManagementClient,
                AzureRestClient,
                KeyVaultManagementClient);
        }

        public void RunPowerShellTest(ServiceManagement.Common.Models.XunitTracingInterceptor logger, params string[] scripts)
        {
            var sf = new StackTrace().GetFrame(1);
            var callingClassType = sf.GetMethod().ReflectedType?.ToString();
            var mockName = sf.GetMethod().Name;

            _helper.TracingInterceptor = logger;
            RunPsTestWorkflow(
                () => scripts,
                // no custom cleanup
                null,
                callingClassType,
                mockName);
        }

        public void RunPsTestWorkflow(
            Func<string[]> scriptBuilder,
            Action cleanup,
            string callingClassType,
            string mockName)
        {

            var d = new Dictionary<string, string>
            {
                {"Microsoft.Resources", null},
                {"Microsoft.Features", null},
                {"Microsoft.Authorization", null},
                {"Microsoft.Network", null},
                {"Microsoft.Compute", null}
            };
            var providersToIgnore = new Dictionary<string, string>
            {
                {"Microsoft.Azure.Management.Resources.ResourceManagementClient", "2016-02-01"}
            };
            HttpMockServer.Matcher = new PermissiveRecordMatcherWithApiExclusion(true, d, providersToIgnore);

            HttpMockServer.RecordsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SessionRecords");

            using (var context = MockContext.Start(callingClassType, mockName))
            {
                SetupManagementClients(context);

                _helper.SetupEnvironment(AzureModule.AzureResourceManager);

                KeyVaultClient = GetKeyVaultClient();
                CosmosDBTestHelper = GetTestHelper();

                var callingClassName = callingClassType.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries).Last();

                _helper.SetupModules(AzureModule.AzureResourceManager,
                    "ScenarioTests\\Common.ps1",
                    "ScenarioTests\\" + callingClassName + ".ps1",
                    _helper.RMProfileModule,
                    _helper.GetRMModulePath("Az.KeyVault.psd1"),
                    _helper.GetRMModulePath("Az.CosmosDB.psd1"),
                    _helper.GetRMModulePath("Az.Network.psd1"),
                    "AzureRM.Resources.ps1");
                try
                {
                    var psScripts = scriptBuilder?.Invoke();
                    if (psScripts != null)
                    {
                        _helper.RunPowerShellTest(psScripts);
                    }
                }
                finally
                {
                    cleanup?.Invoke();
                }
            }
        }

        protected ResourceManagementClient GetResourceManagementClient(MockContext context)
        {
            return context.GetServiceClient<ResourceManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static CosmosDBManagementClient GetCosmosDBManagementClient(MockContext context)
        {
            return context.GetServiceClient<CosmosDBManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static NetworkManagementClient GetNetworkManagementClient(MockContext context)
        {
            return context.GetServiceClient<NetworkManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static AzureRestClient GetAzureRestClient(MockContext context)
        {
            return context.GetServiceClient<AzureRestClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static KeyVaultManagementClient GetKeyVaultManagementClient(MockContext context)
        {
            return context.GetServiceClient<KeyVaultManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }
        private CosmosDBTestHelper GetTestHelper()
        {
            return new CosmosDBTestHelper(this.KeyVaultManagementClient, this.KeyVaultClient);
        }

        private static KeyVaultClient GetKeyVaultClient()
        {
            return new KeyVaultClient(CosmosDBTestHelper.GetAccessToken, CosmosDBTestHelper.GetHandlers());
        }
    }
}
