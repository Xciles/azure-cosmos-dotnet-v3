﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class GatewayAccountReader
    {
        private readonly ConnectionPolicy connectionPolicy;
        private readonly IComputeHash authKeyHashFunction;
        private readonly bool hasAuthKeyResourceToken = false;
        private readonly string authKeyResourceToken = string.Empty;
        private readonly HttpClient httpClient;
        private readonly Uri serviceEndpoint;

        public GatewayAccountReader(Uri serviceEndpoint,
                IComputeHash stringHMACSHA256Helper,
                bool hasResourceToken,
                string resourceToken,
                ConnectionPolicy connectionPolicy,
                HttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.serviceEndpoint = serviceEndpoint;
            this.authKeyHashFunction = stringHMACSHA256Helper;
            this.hasAuthKeyResourceToken = hasResourceToken;
            this.authKeyResourceToken = resourceToken;
            this.connectionPolicy = connectionPolicy;
        }

        private async Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            string authorizationToken = string.Empty;
            if (this.hasAuthKeyResourceToken)
            {
                authorizationToken = HttpUtility.UrlEncode(this.authKeyResourceToken);
            }
            else
            {
                // Retrieve the document service properties.
                string xDate = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.XDate, xDate);

                INameValueCollection headersCollection = new DictionaryNameValueCollection();
                headersCollection.Add(HttpConstants.HttpHeaders.XDate, xDate);

                authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    HttpConstants.HttpMethods.Get,
                    serviceEndpoint,
                    headersCollection,
                    this.authKeyHashFunction);
            }

            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Authorization, authorizationToken);

            using (HttpResponseMessage responseMessage = await this.httpClient.GetHttpAsync(
            serviceEndpoint))
            {
                using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                {
                    return CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
                }
            }
        }

        public async Task<AccountProperties> InitializeReaderAsync()
        {
            AccountProperties databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                this.serviceEndpoint, this.connectionPolicy.PreferredLocations, this.GetDatabaseAccountAsync);

            return databaseAccount;
        }
    }
}
