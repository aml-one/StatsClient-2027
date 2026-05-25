using StatsClient.MVVM.View;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace StatsClient.MVVM.Core;

public class BaseConfigReader
{
    public static string? Dir = Environment.CurrentDirectory;

    public static string ReadBaseSettings()
    {
        string cipherText;
        string keyBase64;
        string vectorBase64;

        if (!File.Exists(Dir + "\\BaseSettings.Config"))
        {
            MessageBox.Show("Could not find config file!\nPlease acquire a BaseSettings.Config file and place it into the application folder.\n\nApplication will shutdown!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SplashWindow.Instance.CloseApp();
            return "";
        }

        using (var streamReader = File.OpenText(Dir + "\\BaseSettings.Config"))
        {
            var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            cipherText = lines[2];
            keyBase64 = lines[3];
            vectorBase64 = lines[4];
        }

        string plainText = DecryptDataWithAes(cipherText, keyBase64, vectorBase64);

        return plainText;
    }



    private static string DecryptDataWithAes(string cipherText, string keyBase64, string vectorBase64)
    {
        using (Aes aesAlgorithm = Aes.Create())
        {
            aesAlgorithm.Key = Convert.FromBase64String(keyBase64);
            aesAlgorithm.IV = Convert.FromBase64String(vectorBase64);

            // Create decryptor object
            ICryptoTransform decryptor = aesAlgorithm.CreateDecryptor();

            byte[] cipher = Convert.FromBase64String(cipherText);

            //Decryption will be done in a memory stream through a CryptoStream object
            using (MemoryStream ms = new MemoryStream(cipher))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }
}
