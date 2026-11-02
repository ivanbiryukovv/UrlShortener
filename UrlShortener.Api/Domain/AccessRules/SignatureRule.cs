using UrlShortener.Api.Services;

namespace UrlShortener.Api.Domain.AccessRules;

public sealed class SignatureRule : LinkAccessRule
{
    private readonly IHmacSigner _signer;

    public SignatureRule(IHmacSigner signer) => _signer = signer;

    public override VisitResult Check(in AccessContext context)
    {
        if (!context.Link.RequiresSignature)
            return VisitResult.Success;

        return _signer.Verify(context.Link.Code, context.Signature)
            ? VisitResult.Success
            : VisitResult.InvalidSignature;
    }
}
