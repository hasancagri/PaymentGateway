using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Iyzico.Provider.StoredCards
{
    public class CardList : ProviderResourceV2
    {
        public string CardUserKey { get; set; }
        public List<Card> CardDetails { get; set; }

        public static Task<CardList> Retrieve(RetrieveCardListRequest request, ProviderOptions options)
        {
            var uri = options.BaseUrl + "/cardstorage/cards";
            return RestHttpClientV2.Create().PostAsync<CardList>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
        }
    }
}
