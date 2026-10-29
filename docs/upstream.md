# Upstream

This template is built on the [fullstackhero .NET starter kit](https://github.com/fullstackhero/dotnet-starter-kit), and it doesn't sync with it: we change the code freely, so a fullstackhero release can't be merged in. Instead, the platform team reviews each fullstackhero release by hand and ports the changes worth having.

## 1. How a release is reviewed

1. When fullstackhero tags a release, list its changes since the last release reviewed, in their repository: `git log --oneline <last>..<new>`, or GitHub's compare view `https://github.com/fullstackhero/dotnet-starter-kit/compare/<last>...<new>`.
2. Decide for each change that matters:
   - **Taken:** port it by hand.
   - **Skipped:** say why, for example because we replaced that part.
   - **Covered:** we already have it, or our own version of it.
3. Port the changes taken in one PR, and add the release and the decisions to section 2 in the same PR.
4. For each change taken, the platform team decides whether the existing product repositories get a PR too.

An upstream security fix, such as a fix for a CVE or a NuGet advisory, is ported at once, without waiting for a release. Their `main` between releases is not followed.

## 2. Releases reviewed

| Release | Reviewed | Decisions |
| --- | --- | --- |
| `10.0.0` | 2026-10-28 | The base of this template, adapted to the platform. Its vulnerable packages were raised to patched versions, and its integration tests run rustfs instead of MinIO |
