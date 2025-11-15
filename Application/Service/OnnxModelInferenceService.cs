using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstractions.Services;
using Application.Model;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Shared.Common;

namespace Worker.Model
{
    /// <summary>
    /// Loads ONNX model once and runs inference using inputs from ITokenizerService.
    /// </summary>
    public sealed class OnnxModelInferenceService : IModelInferenceService, IDisposable
    {
        private readonly ILogger<OnnxModelInferenceService> _logger;
        private readonly ITokenizerService _tokenizer;
        private readonly InferenceSession _session;
        private readonly int _maxSeqLen;
        private readonly string _inputIdsName;
        private readonly string _tokenTypeIdsName;
        private readonly string _attentionMaskName;
        private readonly string _outputName;
        private readonly Microsoft.ML.Tokenizers.BertTokenizer _bertTokenizer;
        private readonly Dictionary<string,int> _vocab;
        private readonly int _clsId;
        private readonly int _sepId;
        private readonly int _padId;

        public OnnxModelInferenceService(IConfiguration cfg, ILogger<OnnxModelInferenceService> logger, ITokenizerService tokenizer)
        {
            _logger = logger;
            _tokenizer = tokenizer;

            var onnxPath = "D:\\TTNVM\\Project\\BE\\Application\\Model\\model.onnx";
            if (!Path.IsPathRooted(onnxPath)) onnxPath = Path.GetFullPath(onnxPath);
            if (!File.Exists(onnxPath)) throw new FileNotFoundException($"ONNX model not found at '{onnxPath}'");

            _maxSeqLen = cfg.GetValue("Model:MaxSeqLen", 128);
            _inputIdsName = cfg["Model:InputIdsName"] ?? "input_ids";
            _tokenTypeIdsName = cfg["Model:TokenTypeIdsName"] ?? "token_type_ids";
            _attentionMaskName = cfg["Model:AttentionMaskName"] ?? "attention_mask";
            _outputName = cfg["Model:OutputName"] ?? "logits";

            _session = new InferenceSession(onnxPath, new SessionOptions());
            _logger.LogInformation("ONNX model loaded: {Path}. MaxSeqLen={Max}", onnxPath, _maxSeqLen);

            var vocabPath = "D:\\TTNVM\\Project\\BE\\Application\\Model\\vocab.txt";
            if (!Path.IsPathRooted(vocabPath)) vocabPath = Path.GetFullPath(vocabPath);
            if (!File.Exists(vocabPath)) throw new FileNotFoundException($"Vocab file not found at '{vocabPath}'");

            var options = new Microsoft.ML.Tokenizers.BertOptions
            {
                LowerCaseBeforeTokenization = false,
                ApplyBasicTokenization = true,
                SplitOnSpecialTokens = true,
            };
            _bertTokenizer = Microsoft.ML.Tokenizers.BertTokenizer.Create(vocabPath, options);

            _vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            int ln = 0; foreach (var line in File.ReadLines(vocabPath)) { var tok = line.Trim(); if (tok.Length == 0) { ln++; continue; } _vocab[tok] = ln; ln++; }
            _padId = _vocab.TryGetValue("[PAD]", out var pad) ? pad : 0;
            _clsId = _vocab.TryGetValue("[CLS]", out var cls) ? cls : 101;
            _sepId = _vocab.TryGetValue("[SEP]", out var sep) ? sep : 102;
        }

        public Task<MberModelRespone> InferAsync(string text)
        {
            text ??= string.Empty;

            var wpTokens = _bertTokenizer.EncodeToTokens(text, out _).Select(t => t.Value).ToList();

            int available = Math.Max(0, _maxSeqLen - 2);
            if (wpTokens.Count > available) wpTokens = wpTokens.Take(available).ToList();

            var allTokens = new List<string>(_maxSeqLen) { "[CLS]" };
            allTokens.AddRange(wpTokens);
            allTokens.Add("[SEP]");
            while (allTokens.Count < _maxSeqLen) allTokens.Add("[PAD]");

            var inputIdsArr = new int[_maxSeqLen];
            var attentionMaskArr = new int[_maxSeqLen];
            var tokenTypeIdsArr = new int[_maxSeqLen];

            for (int i = 0; i < _maxSeqLen; i++)
            {
                var tok = allTokens[i];
                int id = tok switch { "[CLS]" => _clsId, "[SEP]" => _sepId, "[PAD]" => _padId, _ => _vocab.TryGetValue(tok, out var vid) ? vid : _vocab["[UNK]"] };
                inputIdsArr[i] = id;
                attentionMaskArr[i] = tok == "[PAD]" ? 0 : 1;
                tokenTypeIdsArr[i] = 0;
            }

            var inputIds = new DenseTensor<long>(new[] { 1, _maxSeqLen });
            var tokenTypeIds = new DenseTensor<long>(new[] { 1, _maxSeqLen });
            var attentionMask = new DenseTensor<long>(new[] { 1, _maxSeqLen });

            for (int i = 0; i < _maxSeqLen; i++)
            {
                inputIds[0, i] = inputIdsArr[i];
                tokenTypeIds[0, i] = tokenTypeIdsArr[i];
                attentionMask[0, i] = attentionMaskArr[i];
            }

            var feeds = new List<NamedOnnxValue>();
            var sessionInputs = new HashSet<string>(_session.InputMetadata.Keys, StringComparer.Ordinal);

            if (sessionInputs.Contains(_inputIdsName))
                feeds.Add(NamedOnnxValue.CreateFromTensor(_inputIdsName, inputIds));
            else
                throw new InvalidOperationException($"Model does not define input '{_inputIdsName}'. Available: {string.Join(", ", sessionInputs)}");

            if (sessionInputs.Contains(_attentionMaskName))
                feeds.Add(NamedOnnxValue.CreateFromTensor(_attentionMaskName, attentionMask));

            if (sessionInputs.Contains(_tokenTypeIdsName))
                feeds.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeIdsName, tokenTypeIds));

            using var results = _session.Run(feeds);

            var logitsValue = results.FirstOrDefault(v => v.Name.Equals(_outputName, StringComparison.OrdinalIgnoreCase)) ?? results.First();
            var logits = logitsValue.AsTensor<float>();

            int numLabels = logits.Dimensions[^1];
            var probs = new double[numLabels];

            double max = double.NegativeInfinity;
            for (int i = 0; i < numLabels; i++) if (logits[0, i] > max) max = logits[0, i];
            double sum = 0;
            for (int i = 0; i < numLabels; i++) { var e = Math.Exp(logits[0, i] - max); probs[i] = e; sum += e; }
            for (int i = 0; i < numLabels; i++) probs[i] /= sum;

            int argmax = 0; double best = double.MinValue;
            for (int i = 0; i < numLabels; i++) if (probs[i] > best) { best = probs[i]; argmax = i; }

            var Intent = (Intent)argmax;
            var response = new MberModelRespone
            {
                InputText = text,
                Intent = Intent.ToString(),
                ConfidenceScore = best
            };
            return Task.FromResult(response);
        }
        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
