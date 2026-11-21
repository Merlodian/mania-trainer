# mania-trainer
A version of [osu-trainer](https://github.com/FunOrange/osu-trainer) 1.8.0 (by FunOrange) for osu!mania 4K. It shows a beatmap's approximate dan rating (1st–10th, Alpha–Kappa) from the XDC1 difficulty calculator, sets the rate a map needs to reach a chosen dan, and can turn every note into a long note (Full LN) or remove all long notes (No LN).

![](osu-trainer/images/gui.png)

### Additional Notes
+ Full LN doesn't change the dan rating (how much harder it makes a chart is disputed): a Full LN chart is rated, and the dan buttons set rates, as for the original chart. No LN is counted.
+ Maps can be created without alt tabbing with the dan buttons' hotkeys (Alt+Shift+A/S/D/F) and Create Map's (Ctrl+Shift+X)
+ Search "maniatrainer" in osu! to find all your generated maps (they're tagged "osutrainer" too)
+ If you use this application a lot, you can end up using a lot of disk space. (333 mp3s * 3MB/mp3 = 1GB). To free up this space, delete the generated maps in osu, then click the Clean Up button in mania trainer.
+ `tools/calibrate_dans.py` rebuilds the dan thresholds from folders of Dan ~ REFORM ~ beatmaps (Theta, Iota and Kappa come from Dan ~ REFORM ~ FINAL (Ext.)).

Based on osu-trainer by [FunOrange](https://github.com/FunOrange/osu-trainer). Special thanks to [Craftplacer](https://github.com/Craftplacer) for making the UI really nice and pretty

## Licenses
This project uses the following projects:
- [osu-trainer](https://github.com/FunOrange/osu-trainer), the program this one is built from
- XDC1 by TheBagelOfMan, licensed under the MIT License (see XDC1-LICENSE.txt)
- [Font Awesome](https://fontawesome.com/), [licensed under the CC BY 4.0 License](https://creativecommons.org/licenses/by/4.0/)
- [Comfortaa](https://fonts.google.com/specimen/Comfortaa), [licensed under the Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL)
- [osu-resources](https://github.com/ppy/osu-resources), [licensed under the CC BY 4.0 License](https://creativecommons.org/licenses/by-nc/4.0/legalcode)
- [oppai-ng](https://github.com/Francesco149/oppai-ng), [licensed under the Unlicense](https://github.com/Francesco149/oppai-ng/blob/master/UNLICENSE)
- [ProcessMemoryDataFinder](https://github.com/Piotrekol/ProcessMemoryDataFinder), [licensed under GPL-3.0](https://github.com/Piotrekol/ProcessMemoryDataFinder/blob/master/LICENSE)
- [LAME](https://lame.sourceforge.io/)
