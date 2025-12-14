using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using Server.ModelDTO;

namespace Server.Controllers
{
    public class AiController : Base_Control_Api
    {
        private readonly IConfiguration _config;
        public AiController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("Chat-Ai")]
        public async Task<IActionResult> Chat(AiRequestDto request)
        {
            var model = request.Model;
            var client = new ChatClient(model, _config["OpenAI:ApiKey"]);

            var messages = new List<ChatMessage>();

            // 1️⃣ Agrega mensajes anteriores si existen
            if (request.Messages != null)
            {
                foreach (var m in request.Messages)
                {
                    if (m.Role == "user")
                        messages.Add(new UserChatMessage(m.Content));
                    else
                        messages.Add(new AssistantChatMessage(m.Content));
                }
            }

            // 2️⃣ Agrega el último mensaje del usuario
            messages.Add(new UserChatMessage(request.Message));

            // 3️⃣ Llamada al modelo
            var response = await client.CompleteChatAsync(messages);

            var text = response.Value.Content[0].Text;

            return Ok(new AiResponseDto
            {
                Response = text
            });
        }

    }
}
