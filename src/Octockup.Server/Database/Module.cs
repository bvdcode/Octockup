// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octockup.Server.Database
{
    [Table("modules")]
    [Index(nameof(Tag), IsUnique = true)]
    public partial class Module : BaseEntity<Guid>, IImportableEntity
    {
        public Module()
        {
            EncryptedParameters = string.Empty;
        }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("tag")]
        public string Tag { get; set; } = null!;

        [Column("destination")]
        public ModuleDestination Destination { get; set; }

        [Column("backup_module_id")]
        public string BackupModuleId { get; set; } = null!;

        [JsonInclude]
        [Column("parameters")]
        [Obsolete("Use EncryptedParameters instead.")]
        public Dictionary<string, string> Parameters { get; private set; } = [];

        [JsonInclude]
        [Column("encrypted_parameters")]
        public string EncryptedParameters { get; private set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;

        [NotMapped] private Dictionary<string, string>? _paramsCache;
        public EncryptedParamsAccessor Params(IStreamCipher cipher) => new(this, cipher);

        private Dictionary<string, string> LoadParams(IStreamCipher cipher)
        {
            if (_paramsCache is not null)
            {
                return _paramsCache;
            }

            if (string.IsNullOrWhiteSpace(EncryptedParameters))
            {
                return _paramsCache = [];
            }

            byte[] bytes = Convert.FromBase64String(EncryptedParameters);
            string json = cipher.DecryptString(bytes);
            _paramsCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(StringComparer.Ordinal);
            return _paramsCache;
        }

        private void FlushParams(IStreamCipher cipher, Dictionary<string, string> dict)
        {
            Dictionary<string, string> normalized = dict
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            string json = JsonSerializer.Serialize(normalized);
            byte[] bytes = cipher.EncryptString(json);

            EncryptedParameters = Convert.ToBase64String(bytes);
            _paramsCache = normalized;
        }

        void IImportableEntity.RestoreId(Guid id)
        {
            Id = id;
        }
    }
}
