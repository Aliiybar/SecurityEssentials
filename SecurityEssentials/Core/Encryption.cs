﻿using System;
using System.Security.Cryptography;
using System.IO;

namespace SecurityEssentials.Core
{
    /// <summary>
    /// Class to handle the encryption method used to Encrypt data
    /// </summary>
    public sealed class Encryption : IDisposable, SecurityEssentials.Core.IEncryption
    {

        private RijndaelManaged encryptionAlgorithm;

        /// <summary>
        /// Constructor
        /// </summary>
        public Encryption()
        {

            encryptionAlgorithm = new RijndaelManaged();
            encryptionAlgorithm.BlockSize = 128;
            encryptionAlgorithm.KeySize = 256;
            encryptionAlgorithm.Mode = CipherMode.CFB;          // Cipher feedback mode
            encryptionAlgorithm.Padding = PaddingMode.PKCS7;    // How to deal with the padding of blocks
            // require Initialization vector to avoid patterns in input producing patterns in output

        }

        /// <summary>
        /// Decrypt an input byte array and return a string
        /// </summary>
        /// <param name="password">The secret key used to encrypt all data</param>
        /// <param name="iterationCount">The number of iterations of the encryption algorithm used to encrypt data</param>
        /// <param name="salt">The salt used in addition to the encryption key</param>
        /// <param name="input">The byte array to decrypt</param>
        /// <param name="output">The decrypted string</param>
        /// <returns>Success boolean</returns>
        public bool Decrypt(string password, string salt, int iterationCount, string input, out string output)
        {

            byte[] bytKey;
            byte[] bytIV;
            byte[] bytInput = System.Text.Encoding.Unicode.GetBytes(input);
            CreateKey(password, salt, iterationCount, out bytKey, out bytIV);
            ICryptoTransform decryptor = encryptionAlgorithm.CreateDecryptor(bytKey, bytIV);
            var cipher = Convert.FromBase64String(input);

            try
            {
                using (var msDecrypt = new MemoryStream(cipher))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            output = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // http://support.microsoft.com/kb/842791 indicate that an exception 'Padding is invalid and cannot be removed' indicates the decryption has failed
                if (ex.Message.Contains("Padding is invalid and cannot be removed"))
                {
                    output = null;
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;

        }

        public void Dispose()
        {
            encryptionAlgorithm.Dispose();
        }

        /// <summary>
        /// Encrypt an input string and return the encrypted byte array
        /// </summary>
        /// <param name="password">The secret key used to encrypt all data</param>
        /// <param name="iterationCount">The number of iterations of the encryption algorithm used to encrypt data</param>
        /// <param name="salt">The salt used in addition to the encryption key</param>
        /// <param name="input">The input string to encrypt</param>
        /// <param name="output">The encrypted output byte array</param>
        /// <returns>Success boolean</returns>
        public bool Encrypt(string password, string salt, int iterationCount, string input, out string output)
        {

            if (input == null) input = "";
            byte[] bytKey;
            byte[] bytIV;
            CreateKey(password, salt, iterationCount, out bytKey, out bytIV);
            byte[] bytInput = System.Text.Encoding.Unicode.GetBytes(input);

            using (MemoryStream stream = new MemoryStream())
            {
                using (ICryptoTransform encryptor = encryptionAlgorithm.CreateEncryptor(bytKey, bytIV))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new StreamWriter(cryptoStream))
                        {
                            swEncrypt.Write(input);
                        }
                        output = Convert.ToBase64String(stream.ToArray());
                    }
                }
            }
            return true;

        }

        /// <summary>
        /// Creates the encryption key for the parameters passed in
        /// </summary>
        /// <param name="password">The secret key used to encrypt all data</param>
        /// <param name="iterationCount">The number of iterations of the encryption algorithm used to encrypt data</param>
        /// <param name="salt">The salt used in addition to the encryption key</param>
        /// <param name="key">Returned encryption key to use when encrypting the data</param>
        /// <param name="IV">Returned Initialization vector to use when encrypting the data</param>
        private void CreateKey(string password, string salt, int iterationCount, out byte[] key, out byte[] IV)
        {

            Byte[] saltBytes = System.Text.Encoding.Unicode.GetBytes(salt);
            Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltBytes);
            rfc2898DeriveBytes.IterationCount = iterationCount;
            key = rfc2898DeriveBytes.GetBytes(encryptionAlgorithm.KeySize / 8);
            IV = rfc2898DeriveBytes.GetBytes(encryptionAlgorithm.BlockSize / 8);

        }


    }
}