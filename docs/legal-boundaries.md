# Legal boundaries

Rules this codebase holds to. They are not advice, and they are not optional
for contributions. If a change crosses one of these lines, it does not go in.

## What this is

A personal Android app. One person installs it and listens to their own queue.
It is not a service, not a product, and is not on any app store.

## The hard line: no DRM circumvention

**Never add support for a DRM protected service.** Spotify, Apple Music,
Netflix, Tidal, Deezer, Amazon and the rest encrypt their streams. Getting audio
out of them means breaking an access control, which is a circumvention offence
under DMCA section 1201 and its equivalents elsewhere. That is a criminal
statute, and a different category from a terms of service breach.

This is enforced in code, not left to the operator. `DrmSourcePolicy` refuses
those hosts before any resolver runs, and `DrmSourcePolicyTests` proves it.
Removing or weakening that policy is not an accepted change.

Metadata is fine. A Spotify link can be used to look a track up somewhere else.
The audio never comes from Spotify.

## Where this sits with YouTube

The project does not use the YouTube Data API, so the API Developer Policies do
not bind it. It reads links the way a browser does.

That still breaches YouTube's general terms of service, which prohibit automated
access and downloading. Be honest about it rather than pretending otherwise:

- It is a contract breach, not copyright infringement and not a crime.
- The realistic consequence is a blocked IP or a terminated Google account.
- Do not sign into a Google account you care about from this app.

Background playback is the reason the project exists and also the reason the
official API is not an option. The API forbids it outright, in section III.I.9
of the Developer Policies. There is no compliant way to get lock screen playback
out of the embedded player, because that restriction is the paid product.

## Rules for contributions

**Do not turn this into a service or a product.** No hosted instance, no
multi user accounts, no sign up, no app store listing. The moment it serves
strangers it stops being personal use and becomes the thing that got Musi
delisted.

**No credentials in the repo.** No API keys, no cookies, no account tokens, no
session files, no signing keystores. Not in code, not in config, not in tests.

**No piracy framing.** Do not describe this as free Spotify, free YouTube
Premium, or a way to avoid paying for music. Do not add a search or discovery
index that turns it into a catalogue. It plays links the user already has.

**No ad stripping claims.** The project downloads audio for personal playback.
It does not exist to defeat advertising, and should not advertise that it does.

**Keep the DRM list current.** Adding a newly launched DRM service to
`DrmSourcePolicy` is always a welcome change.

## Not for an app store

This cannot go on Google Play. Play's Intellectual Property policy bans apps that
let users download copyrighted works without authorisation, and separately says apps
must not download or access YouTube videos in a way that breaks YouTube's terms. This
app does exactly that, deliberately, which is why it is sideloaded and personal.

Submitting it would also contradict the rule above about not turning it into a
service: a store listing is public distribution.

## If you are asked to take it down

Comply. It is a hobby project. Nothing here is worth a legal fight, and the code
being open source has never been the part anyone objects to.
