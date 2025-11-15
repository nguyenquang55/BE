using Application.Model;
using Shared.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Abstractions.Services
{
    /// <summary>
    /// Performs model inference on input text using tokenizer + ONNX model.
    /// </summary>
    public interface IModelInferenceService
    {
        Task<MberModelRespone>InferAsync(string text);
    }
}
