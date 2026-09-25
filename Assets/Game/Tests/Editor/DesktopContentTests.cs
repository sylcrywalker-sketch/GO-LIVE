using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    public sealed class DesktopContentTests
    {
        [TestCase("Donation", "F27D6227C9C5D170B3791893A2973EC8B450AB82E51E0969CF8FE05DA35F35D7")]
        [TestCase("Outline", "89FB18A055AADE428645F59814C6959E4DADF7EF66118BD936CDB852EA6F2FA7")]
        [TestCase("MyComputer", "8F768E9251FDE0DDC8A175BA1D178AC65EE316E7284D6841DA45D39D73CD4C86")]
        [TestCase("Streamly", "012F82210DED5E068C2F54F0F97BB6A1B96D153F6EF2ECE59E4EAB10A3E5C79D")]
        [TestCase("Trich", "A98A139906088E36E6DF3D6D5FD6616F2EBD1E681D6065963203329A9C5F76B6")]
        [TestCase("Hub", "A3AC8CC855F8517CC3845FCE06B52E3FCD94A468A54A7EB2B21048C3D2972D2A")]
        [TestCase("Web", "F3F0D72E18A2E18F1D728A0F242D2ACB129CE5AA5E122341247DF570C76EFADF")]
        public void FinalIconKeepsTheApprovedSourceAndUiImport(string name, string sha256)
        {
            string path = $"Assets/Game/Art/Desktop/Icons/{name}.png";
            Assert.That(File.Exists(path), Is.True, $"Final supplied {name} icon must be imported.");
            using SHA256 hash = SHA256.Create();
            string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
            Assert.That(actual, Is.EqualTo(sha256), "Do not regenerate, substitute, or interchange the final icons.");
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.InRange(256, 2048));
        }
    }
}
