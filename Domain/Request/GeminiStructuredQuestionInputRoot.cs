namespace SECAnalyzer.Domain.Request
{
    public class Company
    {
        public string type { get; set; }
    }

    public class Content
    {
        public string role { get; set; }
        public List<Part> parts { get; set; }
    }

    public class GenerationConfig
    {
        public int temperature { get; set; }
        public int topK { get; set; }
        public double topP { get; set; }
        public int maxOutputTokens { get; set; }
        public string responseMimeType { get; set; }
        public ResponseSchema responseSchema { get; set; }
    }

    public class Items
    {
        public string type { get; set; }
        public Properties properties { get; set; }
        public List<string> required { get; set; }
    }

    public class Part
    {
        public string text { get; set; }
    }

    public class Properties
    {
        public Response Response { get; set; }
        public Company Company { get; set; }
        public Question Question { get; set; }
    }

    public class Question
    {
        public string type { get; set; }
    }

    public class Response
    {
        public string type { get; set; }
        public Items items { get; set; }
    }

    public class ResponseSchema
    {
        public string type { get; set; }
        public Properties properties { get; set; }
        public List<string> required { get; set; }
    }

    public class GeminiStructuredQuestionInputRoot
    {
        public List<Content> contents { get; set; }
        public SystemInstruction systemInstruction { get; set; }
        public GenerationConfig generationConfig { get; set; }
    }

    public class SystemInstruction
    {
        public string role { get; set; }
        public List<Part> parts { get; set; }
    }
}
