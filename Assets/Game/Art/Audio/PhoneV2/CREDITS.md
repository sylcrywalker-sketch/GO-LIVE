# GO! LIVE Phone V2 audio

Ten original digital UI sounds were synthesized specifically for this project. No recordings, recognizable branded notification melodies, or third-party libraries are included in them.

Source: Docs/PhoneV2/AudioSource/generate_phone_audio.py. Python standard library only. Deterministic 48 kHz mono PCM16 WAV outputs, attack/release envelopes, filtered clicks, DC removal and fixed headroom. The adjacent manifest records dimensions, headroom, clipping and hashes.

The config additionally references existing apartment recordings without copying them:
- TakeOut: ../Apartment/PickupSoft.wav
- PutAway: ../Apartment/Fabric.wav

Those handling clips derive from WasabiWielder, Clothes Rustling 2, CC0, with full provenance preserved in ../Apartment/CREDITS.md and ../Apartment/manifest.json.

Audition the final mix in the physical phone before claiming subjective audio acceptance. The generator's technical checks are not a listening test.
