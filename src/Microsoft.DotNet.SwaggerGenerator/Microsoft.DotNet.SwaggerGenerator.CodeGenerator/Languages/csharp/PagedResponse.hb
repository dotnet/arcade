using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;

namespace {{pascalCaseNs Namespace}}
{
    public static class AsyncEnumerable
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> that, CancellationToken cancellationToken)
        {
            var results = new List<T>();
            using (var enumerator = that.GetEnumerator())
            {
                while (await enumerator.MoveNextAsync(cancellationToken))
                {
                    results.Add(enumerator.Current);
                }
            }
            return results;
        }
    }

    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetEnumerator();
    }

    public interface IAsyncEnumerator<out T> : IDisposable
    {
        T Current { get; }
        Task<bool> MoveNextAsync(CancellationToken cancellationToken);
    }

    public class PagedResponse<T> : IReadOnlyList<T>
    {
        private readonly Func<Request, Response, Task> _onFailure;

        public PagedResponse({{pascalCase Name}} client, Func<Request, Response, Task> onFailure, Response response, IImmutableList<T> values)
        {
            _onFailure = onFailure;
            Client = client;
            Values = values;
            if (!response.Headers.TryGetValues("Link", out var linkHeader))
            {
                return;
            }
            var links = ParseLinkHeader(linkHeader).ToList();
            FirstPageLink = links.FirstOrDefault(t => t.rel == "first").href;
            PrevPageLink = links.FirstOrDefault(t => t.rel == "prev").href;
            NextPageLink = links.FirstOrDefault(t => t.rel == "next").href;
            LastPageLink = links.FirstOrDefault(t => t.rel == "last").href;
        }

        private static IEnumerable<(string href, string rel)> ParseLinkHeader(IEnumerable<string> linkHeader)
        {
            foreach (var header in linkHeader)
            {
                foreach (var link in ParseLinkHeader(header))
                {
                    yield return link;
                }
            }
        }

        private static IEnumerable<(string href, string rel)> ParseLinkHeader(string linkHeader)
        {
            foreach (var link in linkHeader.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ParseLink(link, out var result))
                {
                    yield return result;
                }
            }
        }

        private static bool ParseLink(string link, out (string href, string rel) result)
        {
            result = default;
            var parts = link.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            var href = parts[0].Trim().TrimStart('<').TrimEnd('>');
            var props = new Dictionary<string, string>();
            foreach (var prop in parts.Skip(1))
            {
                if (TryParseProp(prop, out var p))
                {
                    props.Add(p.key, p.value);
                }
            }

            var rel = props["rel"];
            result = (href, rel);
            return true;
        }

        private static bool TryParseProp(string value, out (string key, string value) result)
        {
            result = default;
            var equalIdx = value.IndexOf('=');
            if (equalIdx < 0)
            {
                return false;
            }

            var key = value.Substring(0, equalIdx).Trim();
            var v = value.Substring(equalIdx + 1).Trim().Trim('"');
            result = (key, v);
            return true;
        }

        public string FirstPageLink { get; }
        public string PrevPageLink { get; }
        public string NextPageLink { get; }
        public string LastPageLink { get; }
        public HelixApi Client { get; }

        public IImmutableList<T> Values { get; }

        public async Task<PagedResponse<T>> GetPageAsync(string link, CancellationToken cancellationToken)
        {
            using (var req = Client.Pipeline.CreateRequest())
            {
                req.Uri.Reset(new Uri(link));

                using (var res = await Client.SendAsync(req, cancellationToken).ConfigureAwait(false))
                {
                    if (res.Status < 200 || res.Status >= 300 || res.ContentStream == null)
                    {
                        await _onFailure(req, res).ConfigureAwait(false);
                        throw new InvalidOperationException("Can't get here");
                    }

                    using (var reader = new StreamReader(res.ContentStream))
                    {
                        var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                        return new PagedResponse<T>(Client, _onFailure, res, Client.Deserialize<IImmutableList<T>>(content));
                    }
                }
            }
        }

        public IAsyncEnumerable<T> EnumerateAll()
        {
            return new Enumerable(this);
        }

        private class Enumerable : IAsyncEnumerable<T>
        {
            private readonly PagedResponse<T> _that;

            public Enumerable(PagedResponse<T> that)
            {
                _that = that;
            }

            public IAsyncEnumerator<T> GetEnumerator()
            {
                return new Enumerator(_that);
            }
        }

        private class Enumerator : IAsyncEnumerator<T>
        {
            private PagedResponse<T> _currentPage;
            private IEnumerator<T> _currentPageEnumerator;

            public Enumerator(PagedResponse<T> that)
            {
                _currentPage = that;
                _currentPageEnumerator = _currentPage.GetEnumerator();
            }

            public void Dispose()
            {
                _currentPageEnumerator?.Dispose();
            }

            public T Current => _currentPageEnumerator.Current;

            public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
            {
                if (_currentPageEnumerator.MoveNext())
                {
                    return true;
                }

                if (string.IsNullOrEmpty(_currentPage.NextPageLink))
                {
                    return false;
                }

                _currentPageEnumerator.Dispose();
                _currentPage = await _currentPage.GetPageAsync(_currentPage.NextPageLink, cancellationToken).ConfigureAwait(false);
                _currentPageEnumerator = _currentPage.GetEnumerator();
                return _currentPageEnumerator.MoveNext();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) Values).GetEnumerator();
        }

        public int Count => Values.Count;

        public T this[int index] => Values[index];
    }

}
