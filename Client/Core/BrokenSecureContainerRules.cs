using System;
using System.Collections.Generic;

namespace OperatorTraits.Shared
{
    public static class BrokenSecureContainerRules
    {
        private static readonly HashSet<string> ExplicitlyAllowedTemplates =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "590c60fc86f77412b13fddcf", // Documents case
                "5c093e3486f77430cb02e593", // Dogtag case
                "62a09d3bcf4a99369e262447", // Gingy keychain
                "67d3ed3271c17ff82e0a5b0b", // Key case
                "59fafd4b86f7745ca07e1232", // Key tool
                "619cbf9e0a7c3a1a2731940a", // Keycard holder
                "5d235bb686f77443f4331278", // SICC
                "5783c43d2459774bbe137486", // Wallet
                "60b0f6c058e0b0481a09ad11", // WZ wallet

                "5910968f86f77425cf569c32", // Weapon repair kit

                "59f32bb586f774757e1e8442", "6662e9aca7e0b43baa3d5f74",
                "6662e9cda7e0b43baa3d5f76", "59f32c3b86f77472a31742f0",
                "6662e9f37fa79a6d83730fa0", "6662ea05f6259762c56f3189",
                "675dc9d37ae1a8792107ca96", "675dcb0545b1a2d108011b2b",
                "684180bc51bf8645f7067bc8", "684181208d035f60230f63f9",
                "6764207f2fa5e32733055c4a", "6764202ae307804338014c1a",
                "68418091b5b0c9e4c60f0e7a", "684180ee9b6d80d840042e8a"
            };

        public static bool IsExplicitlyAllowed(string templateId) =>
            !string.IsNullOrEmpty(templateId) &&
            ExplicitlyAllowedTemplates.Contains(templateId);
    }
}
