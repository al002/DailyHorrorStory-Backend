using System.ClientModel;
using System.Text.Json;
using DailyStory.Api.Models;
using DailyStory.Api.Options;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace DailyStory.Api.Services;

public class AiGenerationService : IAiGenerationService
{
    private readonly OpenAIClient _client;
    private readonly AiServiceOptions _options;
    private readonly ILogger<AiGenerationService> _logger;

    public AiGenerationService(IOptionsMonitor<AiServiceOptions> optionsAccessor, ILogger<AiGenerationService> logger)
    {
        _options = optionsAccessor.CurrentValue;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.OpenRouterApiKey))
        {
            throw new InvalidOperationException("OpenRouter API Key not set");
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException("Openrouter BaseUrl not set");
        }
        
        var options = new OpenAIClientOptions()
        {
            Endpoint = new Uri(_options.BaseUrl)
        };
        
        _client = new OpenAIClient(new ApiKeyCredential(_options.OpenRouterApiKey), options);
        _logger.LogInformation("AiGenerationService initialized, BaseUrl: {OptionsBaseUrl}", _options.BaseUrl);
    }
    
    public async Task<(string Title, string Content)> GenerateStoryAsync(string? theme = null, string? modelName = null, CancellationToken cancellationToken = default)
    {
        var modelToUse = modelName ?? _options.Model;

        if (string.IsNullOrWhiteSpace(modelToUse))
        {
            _logger.LogError("Model is not specified");
            throw new InvalidOperationException("Model is not specified");
        }
        
        _logger.LogInformation("Use Model '{ModelName}'", modelToUse);

        string jsonSchema = """
                            {
                              "title": "故事的标题",
                              "content": "故事的完整内容"
                            }
                            """;

        // System Prompt
        string systemPrompt = $"""
                               你是一位才华横溢的短篇小说作家。
                               你的任务是创作一个引人入胜的短篇恐怖中文故事，最好是中式恐怖，要完成完整的起承转合，要有让人后怕的感觉。
                               你必须严格按照以下 JSON 结构响应，并且只响应 JSON 对象，不包含任何额外的解释性文字、代码块标记或任何其他非 JSON 内容。
                               JSON 结构:
                               {jsonSchema}
                               """;
        string userPrompt = $"""
                             请输出今天的短篇恐怖故事，随机选择一个主题。
                             请确保你的响应是一个有效的 JSON 对象，完全符合我之前提供的结构，只包含 'title' 和 'content' 字段。
                             """;
        
        var chatMessages = new List<ChatMessage>()
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt),
        };

        var completionOptions = new ChatCompletionOptions()
        {
            MaxOutputTokenCount = _options.MaxTokens,
            Temperature = (float?)_options.Temperature,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            ChatClient chatClient = _client.GetChatClient(modelToUse);
            ClientResult<ChatCompletion> response = await chatClient.CompleteChatAsync(chatMessages, completionOptions, cancellationToken);
            
            if (response.Value.Content != null && response.Value.Content.Any())
            {
                StoryGenerationResult? result = null;
                try
                {
                    result = JsonSerializer.Deserialize<StoryGenerationResult>(response.Value.Content[0].Text,
                        new JsonSerializerOptions()
                        {
                            PropertyNameCaseInsensitive = true,
                        });
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError("Cannot parse AI response to json, original response, {res}", response.Value.Content[0].Text);
                    throw new ApplicationException($"'{modelToUse}' returned invalid json structure");
                }

                if (result == null || string.IsNullOrWhiteSpace(result?.Title) ||
                    string.IsNullOrWhiteSpace(result?.Content))
                {
                    _logger.LogWarning("Response JSON has no title or content");
                    throw new ApplicationException("Response JSON has no title or content");
                } 
                
                _logger.LogInformation("Result title: {Title}", result.Title);
                return (result.Title, result.Content);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "在 AI 故事生成过程中 (模型: {ModelName}) 发生意外错误。原始响应: {RawResponse}", modelToUse, "N/A");
            throw;
        }
        
        throw new InvalidOperationException("代码逻辑错误：未能从 GenerateStoryAsync 方法的所有路径返回有效结果或抛出异常。");
    }
}