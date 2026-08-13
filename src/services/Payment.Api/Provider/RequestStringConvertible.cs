using System;

namespace Payment.Api.Provider
{
    public interface RequestStringConvertible
    {
        String ToPKIRequestString();
    }
}
