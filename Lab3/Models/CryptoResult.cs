// Lab3/Models/CryptoResult.cs
using System.Collections.Generic;
using System.Numerics;

public class CryptoResult
{
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? BinaryData { get; set; }

    public object? Payload { get; set; }

    public List<BigInteger>? PrivateKey { get; set; }
    public List<BigInteger>? PublicKey { get; set; }
    public BigInteger Multiplier { get; set; }
    public BigInteger Modulus { get; set; }

    public static CryptoResult Ok(string data) =>
        new() { Success = true, Data = data };

    public static CryptoResult Ok(string message, byte[] binaryData) =>
    new() { Success = true, Data = message, BinaryData = binaryData };


    public static CryptoResult Ok(byte[] imageData) =>
        new() { Success = true, BinaryData = imageData };

    public static CryptoResult Ok(string data, List<BigInteger> priv, List<BigInteger> pub, BigInteger w, BigInteger n) =>
        new()
        {
            Success = true,
            Data = data,
            PrivateKey = priv,
            PublicKey = pub,
            Multiplier = w,
            Modulus = n
        };

    public static CryptoResult Ok(string data, object payload) =>
    new() { Success = true, Data = data, Payload = payload };

    public static CryptoResult Error(string message) =>
        new() { Success = false, ErrorMessage = message };
}