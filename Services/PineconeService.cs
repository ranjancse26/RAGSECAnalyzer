using Pinecone;

namespace SECAnalyzer.Services
{
    public interface IPineconeService
    {
        Task<Pinecone.Index> GetIndex(string indexName);
        Task<Pinecone.Index> CreateIndexAsync(
           string indexName, int dimensions,
           CreateIndexRequestMetric metric,
           ServerlessSpec spec);
        Task<(UpsertResponse, string)> Vectorize(
            string indexHost, List<float> vectorData, Metadata metaData);
        Task<QueryResponse> Query(string indexHost, List<float> vectorData,
            Metadata metaData, int topK = 10);
    }

    /// <summary>
    /// Pinecone Service
    /// </summary>
    public class PineconeService : IPineconeService
    {
        private readonly string apiKey;
        public PineconeService(string apiKey) {
            this.apiKey = apiKey;
        }

        public async Task<Pinecone.Index> GetIndex(string indexName)
        {
            var pinecone = new PineconeClient(apiKey);
            try
            {
                var index = await pinecone.DescribeIndexAsync(indexName);
                return index;
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<Pinecone.Index> CreateIndexAsync(
           string indexName,
           int dimensions,
           CreateIndexRequestMetric metric,
           ServerlessSpec spec)
        {
            try
            {
                var pinecone = new PineconeClient(apiKey);

                var index = await pinecone.CreateIndexAsync(new CreateIndexRequest
                {
                    Name = indexName,
                    Dimension = dimensions,
                    Metric = metric,
                    Spec = new ServerlessIndexSpec
                    {
                        Serverless = spec
                    },
                    DeletionProtection = DeletionProtection.Enabled
                });

                var description = await pinecone.DescribeIndexAsync(indexName);
                return description;
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        public async Task<QueryResponse> Query(string indexHost, List<float> vectorData,
            Metadata metaData, int topK = 10)
        {
            var pinecone = new PineconeClient(apiKey);
            var idx = pinecone.Index(host: indexHost);
            var results = await idx.QueryAsync(
                new QueryRequest
                {
                    Vector = vectorData.ToArray(),
                    Filter = metaData,
                    IncludeValues = false,
                    IncludeMetadata = true,
                    TopK = (uint)topK
                }
            );
            return results;
        }

        public async Task<(UpsertResponse,string)> Vectorize(
            string indexHost, List<float> vectorData, Metadata metaData)
        {
            var pinecone = new PineconeClient(apiKey);
            var idx = pinecone.Index(host: indexHost);
            string uniqueId = Guid.NewGuid().ToString();
            var vectors = new List<Vector>
            {
                new Vector { 
                    Id = uniqueId,
                    Metadata = metaData,
                    Values = vectorData.ToArray()
                }
            };

            var upsertResponse = await idx.UpsertAsync(
                new UpsertRequest { Vectors = vectors }
            );

            return (upsertResponse, uniqueId);
        }
    }
}
