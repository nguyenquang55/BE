using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Shared.Common;

namespace Infrastructure.Model
{
    public class BertTokenizerService : ITokenizerService
    {
        public Task<Result<BertTokenizer>> Tokenizer(string VocabPath)
        {
            var options = new BertOptions
            {
                LowerCaseBeforeTokenization = false,
                ApplyBasicTokenization = true,
                SplitOnSpecialTokens = true,
            };

            var tokenizer = BertTokenizer.Create(VocabPath, options);
            return Task.FromResult(Result<BertTokenizer>.SuccessResult(tokenizer));
        }
    }
}