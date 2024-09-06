// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

#if !NET472_OR_GREATER

namespace Microsoft.DotNet.Build.Tasks.Feed;


public class TokenCredentialShortCache : TokenCredential
{
    private const int CacheExpirationMinutes = 3;

    public TokenCredentialShortCache(TokenCredential tokenCredential)
    {
        _tokenCredential = tokenCredential;
    }

    private TokenCredential _tokenCredential;
    private ConcurrentDictionary<CacheKey, CachedToken> _tokenCache = new ConcurrentDictionary<CacheKey, CachedToken>();

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).Result;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        CacheKey cacheKey = new CacheKey
        {
            Scopes = String.Join(":", requestContext.Scopes),
            Claims = requestContext.Claims,
            TenantId = requestContext.TenantId,
            IsCaeEnabled = requestContext.IsCaeEnabled
        };

        bool doCleanUpCache = false;

        CachedToken cachedToken = _tokenCache.GetOrAdd(cacheKey, _ => new CachedToken());
        var token = await cachedToken.GetToken(requestContext, () => {
            // initiate cleanup of cache only when we are going to get any fresh token
            doCleanUpCache = true;
            return _tokenCredential.GetTokenAsync(requestContext, cancellationToken);
        });


        if (doCleanUpCache)
        {
            // go over all the cache items and remove the ones that are eligible to remove
            _tokenCache.Keys.ToList().ForEach(key =>
            {
                if (_tokenCache.TryGetValue(key, out CachedToken ct))
                {
                    if (ct.EligibleToRemove)
                    {
                        _tokenCache.TryRemove(key, out _);
                    }
                }
            });
        }

        return token;
    }

    private record struct CacheKey
    {
        public required string Scopes { get; init; }
        public required string Claims { get; init; }
        public required string TenantId { get; init; }
        public required bool IsCaeEnabled { get; init; }
    }

    private class CachedToken
    {
        private AccessToken? token { get; set; }
        private DateTime shortTimeCacheExpiresOn = DateTime.UtcNow.AddMinutes(CacheExpirationMinutes);
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public bool EligibleToRemove => shortTimeCacheExpiresOn.AddMinutes(CacheExpirationMinutes) < DateTime.UtcNow;

        public async ValueTask<AccessToken> GetToken(TokenRequestContext requestContext, Func<ValueTask<AccessToken>> getFreshToken)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (token != null && shortTimeCacheExpiresOn > DateTime.UtcNow)
                {
                    return token.Value;
                }

                token = await getFreshToken();
                shortTimeCacheExpiresOn = DateTime.UtcNow.AddMinutes(CacheExpirationMinutes);
                return token.Value;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}

#endif
