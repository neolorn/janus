# Round H7: a signing key outliving its host

## 1. Items implemented

- The signing key a stopped host disposed (CONV-CODE-007 AC4, AUTH-KEY-001):
  - the defect: a host disposes each private key object at its stop (CONV-CODE-007),
    while the token library's shared factory keeps a signature provider per key,
    keyed by the key and not by the object, for the life of the process; a second host
    started in the process on the same stored key made a new object, was handed the
    first host's provider, and signed through the disposed object, so its authorization
    requests answered 500;
  - the fix: each signing key's credential carries a factory of its own with signature
    providers not cached, so every signature is made through the object the host holds
    now, and no provider outlives the object it wraps;
  - why this and not keeping the object alive: CONV-CODE-007 has the private key object
    disposed at the key's retirement and at the stop, and a host cannot know when the
    shared cache would let go of a provider; turning the cache off for these credentials
    alone keeps the disposal, leaves the shared factory as it is for every other key,
    and costs one provider made per signature;
  - `SigningKeyRotationTests.CONV_CODE_007_AC4_AHostStartedAfterAnotherStoppedSignsWithItsOwnKeyObjectAsync`:
    two hosts on one key store, started and stopped in turn in one process; the second
    signs an access token with the same key and accepts it at `/oidc/userinfo`. It
    failed before the fix with a 500 at the authorization request.

## 2. Items not implemented

None.

## 3. Resolved by rule

None.

## 4. Open questions

None.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: 5205, green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
