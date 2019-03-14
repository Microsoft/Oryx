﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Oryx.Integration.Tests.LocalDockerTests
{
    public class MySqlIntegrationTests : DatabaseTestsBase, IClassFixture<Fixtures.MySqlDbContainerFixture>
    {
        private const int hostPort = 8086;

        public MySqlIntegrationTests(ITestOutputHelper output, Fixtures.MySqlDbContainerFixture dbFixture) : base(output, dbFixture, hostPort)
        {
        }

        [Fact]
        public async Task NodeApp_MySqlDB()
        {
            await RunTestAsync(
                "nodejs",  "10.14",
                Path.Combine(HostSamplesDir, "nodejs", "node-mysql"),
                _dbFixture.DbServerContainerName);
        }

        [Fact]
        public async Task Python37App_MySqlDB_UsingPyMySql()
        {
            await RunTestAsync(
                "python", "3.7",
                Path.Combine(HostSamplesDir, "python", "mysql-pymysql-sample"),
                _dbFixture.DbServerContainerName);
        }

        [Fact]
        public async Task Python37App_MySqlDB_UsingMySqlConnector()
        {
            await RunTestAsync(
                "python", "3.7",
                Path.Combine(HostSamplesDir, "python", "mysql-mysqlconnector-sample"),
                _dbFixture.DbServerContainerName);
        }

        [Fact]
        public async Task Python37App_MySqlDB_UsingMySqlClient()
        {
            await RunTestAsync(
                "python", "3.7",
                Path.Combine(HostSamplesDir, "python", "mysql-mysqlclient-sample"),
                _dbFixture.DbServerContainerName);
        }
    }
}