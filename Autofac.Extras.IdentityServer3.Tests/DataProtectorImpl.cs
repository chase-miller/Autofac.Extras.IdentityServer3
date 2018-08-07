using System;
using IdentityServer3.Core.Configuration;

namespace Autofac.Extras.IdentityServer3.Tests
{
    class DataProtectorImpl : IDataProtector
    {
        public byte[] Protect(byte[] data, string entropy = "")
        {
            throw new NotImplementedException();
        }

        public byte[] Unprotect(byte[] data, string entropy = "")
        {
            throw new NotImplementedException();
        }
    }
}