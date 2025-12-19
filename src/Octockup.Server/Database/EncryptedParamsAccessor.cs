// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.Abstractions;

namespace Octockup.Server.Database
{
    public partial class Module
    {
        public readonly struct EncryptedParamsAccessor
        {
            private readonly Module _module;
            private readonly IStreamCipher _cipher;

            internal EncryptedParamsAccessor(Module m, IStreamCipher c) { _module = m; _cipher = c; }

            public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_module.LoadParams(_cipher));

            public string? Get(string key) => _module.LoadParams(_cipher).TryGetValue(key, out var v) ? v : null;

            public void Set(string key, string value)
            {
                var d = _module.LoadParams(_cipher);
                d[key] = value;
                _module.FlushParams(_cipher, d);
            }

            public bool Remove(string key)
            {
                var d = _module.LoadParams(_cipher);
                var ok = d.Remove(key);
                if (ok) _module.FlushParams(_cipher, d);
                return ok;
            }

            public string this[string key]
            {
                get => _module.LoadParams(_cipher)[key];
                set
                {
                    var d = _module.LoadParams(_cipher);
                    d[key] = value;
                    _module.FlushParams(_cipher, d);
                }
            }
        }
    }
}
