using Jina;

namespace SECAnalyzer.Services
{
    public interface IJinaEmbeddingService
    {
        void SetEmbeddingModel(string modelName);
        Task<ModelEmbeddingOutput> GetEmbeddingOutputAsync(
            List<string> contents);
    }

    /// <summary>
    /// Jina Embedding Service
    /// </summary>
    public class JinaEmbeddingService : IJinaEmbeddingService
    {
        private string EmbeddingModel = "jina-embeddings-v3";
        private readonly JinaApi jinaApi;
        public JinaEmbeddingService(string apiKey)
        {
            this.jinaApi = GetAuthenticatedApi(apiKey);
        }

        JinaApi GetAuthenticatedApi(string apiKey)
        {
            var api = new JinaApi(apiKey);
            return api;
        }

        public void SetEmbeddingModel(string modelName)
        {
            EmbeddingModel = modelName;
        }

        private List<ApiSchemasEmbeddingTextDoc> GetApiSchemasEmbeddingTextDoc(List<string> contents)
        {
            var apiSchemasEmbeddingTextDocuments = new List<ApiSchemasEmbeddingTextDoc>();
            foreach (var content in contents)
            {
                apiSchemasEmbeddingTextDocuments.Add(new ApiSchemasEmbeddingTextDoc { Text = content });
            }
            return apiSchemasEmbeddingTextDocuments;
        }

        public async Task<ModelEmbeddingOutput> GetEmbeddingOutputAsync(
            List<string> contents)
        {
            var embeddingTextDocs = GetApiSchemasEmbeddingTextDoc(contents);
            var response = await jinaApi.Embeddings.CreateEmbeddingAsync(
                new TextEmbeddingInput
            {
                Model = EmbeddingModel,
                Input = embeddingTextDocs
            });
            return response;
        }
    }
}
