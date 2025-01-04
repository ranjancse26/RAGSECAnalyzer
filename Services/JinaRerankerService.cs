using Jina;

namespace SECAnalyzer.Services
{
    public interface IJinaRerankerService
    {
        void SetRerankerModel(string modelName);
        Task<List<RankingOutputResult>> Rerank(List<string> contents,
            int top, string query);
    }

    /// <summary>
    /// JINA Reranker Service
    /// </summary>
    public class JinaRerankerService : IJinaRerankerService
    {
        private string ReRankerModel = "jina-reranker-v2-base-multilingual";
        private readonly JinaApi jinaApi;
        public JinaRerankerService(string apiKey)
        {
            this.jinaApi = GetAuthenticatedApi(apiKey);
        }

        JinaApi GetAuthenticatedApi(string apiKey)
        {
            var api = new JinaApi(apiKey);
            return api;
        }

        public void SetRerankerModel(string modelName)
        {
            ReRankerModel = modelName;
        }

        public async Task<List<RankingOutputResult>> Rerank(List<string> contents, 
            int top, string query)
        {
            var rerankResponse = new List<RankingOutputResult>();
            RankingOutput output = await jinaApi.Rerank.RankAsync(
               model: ReRankerModel,
               query: query,
               topN: top,
               documents: contents);

            return output.Results.ToList();
        }
    }
}
