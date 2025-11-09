using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Infrastructure
{
    public interface IRedisCacheService
    {
        #region String Cache
        /// <summary>
        /// Lấy object từ Redis theo key (dạng JSON string).
        /// </summary>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Lưu object vào Redis theo key (serialize sang JSON string).
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        #endregion

        #region Hash Cache
        /// <summary>
        /// Lưu nhiều field-value vào Redis Hash theo key.
        /// </summary>
        Task HashSetAsync(string key, Dictionary<string, string> values, TimeSpan? expiry = null);

        /// <summary>
        /// Lưu field-value vào Redis Hash theo key.
        /// </summary>
        Task HashSetAsync(string key, string field, string value, TimeSpan? expiry = null);

        /// <summary>
        /// Lấy giá trị của 1 field trong Redis Hash.
        /// </summary>
        Task<string?> HashGetAsync(string key, string field);

        /// <summary>
        /// Lấy toàn bộ field-value trong Redis Hash theo key.
        /// </summary>
        Task<Dictionary<string, string>?> HashGetAllAsync(string key);

        /// <summary>
        /// Xoá 1 field trong Redis Hash.
        /// </summary>
        Task HashRemoveAsync(string key, string field);
        #endregion

        #region Common Remove
        /// <summary>
        /// Xoá 1 key trong Redis (dạng String hoặc Hash).
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Xoá nhiều key trong Redis theo pattern.
        /// Lưu ý: cần cấu hình Redis `allow-admin` để dùng được Keys(pattern).
        /// </summary>
        Task RemoveByPatternAsync(string pattern);
        #endregion
    }
}
