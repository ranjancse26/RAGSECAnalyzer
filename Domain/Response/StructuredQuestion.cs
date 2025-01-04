namespace SECAnalyzer.Domain.Response
{
    public class StructuredQuestionRoot
    {
        public List<StructuredQuestion> Response { get; set; }  
    }

    public class StructuredQuestion
    {
        public string Company { get; set; }
        public string Question { get; set; }
    }
}
