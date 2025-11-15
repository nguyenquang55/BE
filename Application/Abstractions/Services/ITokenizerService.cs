using System.Collections.Generic;
using Microsoft.ML.Tokenizers;
using Shared.Common;


namespace Application.Abstractions.Services
{
    /// <summary>
    /// Tokenizer service for BERT-like models. Should be initialized once and reused (singleton).
    /// </summary>
    public interface ITokenizerService
    {
        Task <Result<BertTokenizer>> Tokenizer (string VocabPath);
    }
}
