# Apartment V3 audio

All eight added clips use Creative Commons Zero (CC0). No paid asset or new package is required.

- **Footstep_HardFloor_01.wav through Footstep_HardFloor_05.wav** derive from `footstep_concrete_000.ogg` through `footstep_concrete_004.ogg`, Kenney, **Impact Sounds 1.0**. They are used as quiet hard-surface shoes on the explicitly assigned bathroom floor collider. [Official source and CC0 declaration](https://kenney.nl/assets/impact-sounds).
- **Neighbour_WoodImpact_01.wav and Neighbour_WoodImpact_02.wav** derive from `impactWood_light_000.ogg` and `impactWood_light_002.ogg` in the same Kenney pack. They represent rare wooden contacts beyond the shared wall; a spatial low-pass supplies the muffling. They contain no speech or invented voices.
- **FloorCreak_01.wav** derives from **Wood Creak Single V9** by **Rudmer_Rotteveel**. [Original sound page and CC0 declaration](https://freesound.org/people/Rudmer_Rotteveel/sounds/506664/). The public HQ MP3 preview was decoded to mono 48 kHz PCM16, filtered at 90-4200 Hz, gently edge-faded and attenuated to a -17 dBFS peak. It is an actual wooden-floor recording, not a door sound. The WAV does not recover detail absent from the MP3 preview.

Kenney excerpts were decoded without another lossy encode, mixed to mono with equal channel amplitudes, given a 2 ms attack fade and conservative peaks (-6 dBFS footsteps / -13 dBFS contacts), then stored as 48 kHz PCM16. These changes prevent decoder/downmix overs and leave mixing headroom. None of the resulting WAV files is claimed to be byte-identical to the source OGG.

[CC0 legal text](https://creativecommons.org/publicdomain/zero/1.0/). Attribution is retained for provenance; CC0 does not require it. `manifest.json` records source hashes, transformations, duration, peaks and clipping checks. The complete archive and source-page evidence are outside Assets in `Library/ApartmentV3AudioSources`.

The available audio-input tool rejected the audition request as unsupported. Technical checks are complete; subjective in-game listening is not claimed. Verify the mix through speakers/headphones before final acceptance.
