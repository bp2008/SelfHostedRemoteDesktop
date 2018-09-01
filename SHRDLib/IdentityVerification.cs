using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.SimpleHttp;

namespace SHRDLib
{
	/// <summary>
	/// A static class providing methods for a client to verify its identity to a server with public key encryption.
	/// This only verifies that the client has a certain private key.  It does not verify that the communication isn't being intercepted and read by a 3rd-party.
	/// This is used as the primary means to authenticate a SelfHostedRemoteDesktop client to the server.
	/// For the server to authenticate itself, it is recommended to use standard HTTPs with a domain name and a verifiable certificate.
	/// </summary>
	public static class IdentityVerification
	{
		private static X509Certificate2 identify_verification_cert = null;

		/// <summary>
		/// Signs a byte array so that another party can verify we own our private key.
		/// </summary>
		/// <param name="challenge"></param>
		/// <returns></returns>
		public static byte[] SignAuthenticationChallenge(byte[] challenge)
		{
			byte[] sha1 = Hash.GetSHA1Bytes(challenge);
			// I'm not sure if it is actually necessary to synchronize access to the RSACryptoServiceProvider
			lock (certLock)
			{
				EnsureClientCertificateExists();
				RSACryptoServiceProvider csp = (RSACryptoServiceProvider)identify_verification_cert.PrivateKey;
				byte[] sig = csp.SignHash(sha1, CryptoConfig.MapNameToOID("SHA1"));
				return sig;
			}
		}

		/// <summary>
		/// Verifies that another party owns the private key they claim to own by verifying their signature.
		/// </summary>
		/// <param name="challenge">The original byte array that was sent as a challenge.</param>
		/// <param name="publicKey">The public key the other party claims to have the private key for.  In XML format.</param>
		/// <param name="signature">The signature we are attempting to verify.</param>
		/// <returns></returns>
		public static bool VerifySignature(byte[] challenge, string publicKey, byte[] signature)
		{
			RSACryptoServiceProvider key = new RSACryptoServiceProvider();
			key.FromXmlString(publicKey);
			return VerifySignature(challenge, key, signature);
		}

		/// <summary>
		/// Verifies that another party owns the private key they claim to own by verifying their signature.
		/// </summary>
		/// <param name="challenge">The original byte array that was sent as a challenge.</param>
		/// <param name="publicKey">The public key the other party claims to have the private key for.</param>
		/// <param name="signature">The signature we are attempting to verify.</param>
		/// <returns></returns>
		public static bool VerifySignature(byte[] challenge, RSACryptoServiceProvider publicKey, byte[] signature)
		{
			byte[] sha1 = Hash.GetSHA1Bytes(challenge);
			return publicKey.VerifyHash(sha1, CryptoConfig.MapNameToOID("SHA1"), signature);
		}

		private static object certLock = new object();
		/// <summary>
		/// Loads and/or creates the self-signed client certificate for this program.
		/// </summary>
		public static void EnsureClientCertificateExists()
		{
			if (identify_verification_cert == null)
			{
				lock (certLock)
				{
					if (identify_verification_cert == null)
					{
						identify_verification_cert = GetIdentityVerificationCertificate();
					}
				}
			}
		}
		/// <summary>
		/// Returns the public key XML string.
		/// </summary>
		/// <returns></returns>
		public static string GetPublicKeyXML()
		{
			lock (certLock)
			{
				EnsureClientCertificateExists();
				RSACryptoServiceProvider csp = (RSACryptoServiceProvider)identify_verification_cert.PublicKey.Key;
				return csp.ToXmlString(false);
			}
		}

		/// <summary>
		/// Gets the existing identity verification certificate if it exists, otherwise creates a new one with 1000 year expiration (actually, the expiration date shouldn't matter because this isn't verified by DNS address).
		/// </summary>
		/// <returns></returns>
		private static X509Certificate2 GetIdentityVerificationCertificate()
		{
			X509Certificate2 ssl_certificate;
			FileInfo fiCert = new FileInfo(Globals.WritableDirectoryBase + "SHRD-ClientCert.pfx");
			if (fiCert.Exists)
				ssl_certificate = new X509Certificate2(fiCert.FullName, "N0t_V3ry-S3cure#lol");
			else
			{
				using (BPUtil.SimpleHttp.Crypto.CryptContext ctx = new BPUtil.SimpleHttp.Crypto.CryptContext())
				{
					ctx.Open();

					ssl_certificate = ctx.CreateSelfSignedCertificate(
						new BPUtil.SimpleHttp.Crypto.SelfSignedCertProperties
						{
							IsPrivateKeyExportable = true,
							KeyBitLength = 4096,
							Name = new X500DistinguishedName("cn=localhost"),
							ValidFrom = DateTime.Today.AddDays(-1),
							ValidTo = DateTime.Today.AddYears(1000),
						});

					byte[] certData = ssl_certificate.Export(X509ContentType.Pfx, "N0t_V3ry-S3cure#lol");
					File.WriteAllBytes(fiCert.FullName, certData);
				}
			}
			return ssl_certificate;
		}
	}
}
