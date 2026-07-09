using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using Server.ModelDTO;

namespace Server.Controllers
{
    [Authorize]
    public class AiController : Base_Control_Api
    {
        private readonly IConfiguration _config;

        public AiController(IConfiguration config)
        {
            _config = config;
        }


        [HttpPost("Chat-Ai")]
        public async Task<IActionResult> Chat([FromBody] AiRequestDto request)
        {


            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message is required");

            var client = new ChatClient(
                request.Model ?? "gpt-4.1-mini",
                _config["OpenAI:ApiKey"]
            );

            var messages = new List<ChatMessage>
            {
                // 0️⃣ SYSTEM PROMPT
                new SystemChatMessage(SystemPrompt)
            };

            // 1️⃣ Mensajes anteriores
            if (request.Messages != null)
            {
                foreach (var m in request.Messages)
                {
                    if (m.Role == "user")
                        messages.Add(new UserChatMessage(m.Content));
                    else if (m.Role == "assistant")
                        messages.Add(new AssistantChatMessage(m.Content));
                }
            }

            // 2️⃣ Último mensaje del usuario
            messages.Add(new UserChatMessage(request.Message));

            // 3️⃣ Llamada al modelo
            var response = await client.CompleteChatAsync(messages);

            // 4️⃣ Extraer texto de forma segura
            var content = response.Value.Content
                .FirstOrDefault(c => c.Kind == ChatMessageContentPartKind.Text)?
                .Text ?? string.Empty;

            return Ok(new AiResponseDto
            {
                Content = content,
                ImageUrl = null
            });
        }

        private const string SystemPrompt =
            "You are a helpful AI assistant.\n\n" +
            "Rules:\n" +
            "- Always respond using Markdown.\n" +
            "- Never return raw HTML.\n" +
            "- Use Markdown links.\n" +
            "- If an image is relevant, describe it and ask the user if they want it generated.\n" +
            "- Keep responses concise and structured.";


    }
}
