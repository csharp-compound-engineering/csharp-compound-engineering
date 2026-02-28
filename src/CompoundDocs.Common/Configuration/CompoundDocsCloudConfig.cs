namespace CompoundDocs.Common.Configuration;

public class CompoundDocsCloudConfig
{
    public AwsConfig Aws { get; set; } = new();
    public NeptuneConfig Neptune { get; set; } = new();
    public OpenSearchConfig OpenSearch { get; set; } = new();
    public BedrockConfig Bedrock { get; set; } = new();
    public List<RepositoryConfig> Repositories { get; set; } = [];
    public AuthConfig Auth { get; set; } = new();
    public GraphRagConfig GraphRag { get; set; } = new();
}

public class AwsConfig
{
    public string Region { get; set; } = "us-east-1";
}

public class NeptuneConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public int Port { get; set; } = 8182;
}

public class OpenSearchConfig
{
    public string CollectionEndpoint { get; set; } = string.Empty;
    public string IndexName { get; set; } = "compound-docs";
}

public class BedrockConfig
{
    public string EmbeddingModelId { get; set; } = "amazon.titan-embed-text-v2:0";
    public string SonnetModelId { get; set; } = "anthropic.claude-sonnet-4-5-v1:0";
    public string HaikuModelId { get; set; } = "anthropic.claude-haiku-4-5-v1:0";
    public string OpusModelId { get; set; } = "anthropic.claude-opus-4-5-v1:0";
}

public class RepositoryConfig
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string[] MonitoredPaths { get; set; } = [];
}

public class AuthConfig
{
    public Dictionary<string, string> ApiKeys { get; set; } = [];
}

public class GraphRagConfig
{
    public int MaxTraversalSteps { get; set; } = 5;
    public int MaxChunksPerQuery { get; set; } = 10;
    public double MinRelevanceScore { get; set; } = 0.7;
    public bool UseCrossRepoLinks { get; set; } = true;
}
