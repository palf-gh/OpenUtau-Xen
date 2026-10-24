# Persistence and plugin review

## Native envelope validation

The native document DTO previously initialised a valid format/version and an empty project. Since the shared YAML reader ignores unknown properties, an empty mapping or a legacy USTX document renamed `.ustxen` could therefore be accepted as an empty native song. The DTO now starts with missing format/project and version zero. Serialization writes all three envelope members explicitly; parsing rejects missing or unsupported members before initialising the project.

Regression cases cover empty content, `{}`, omitted/null project, omitted format/version and a plain legacy project passed to the native reader. Existing round-trip coverage verifies that valid documents still serialise their complete envelope.

## Partial legacy-plugin pitch output

UST plugin output may omit pitch fields or return only `PBY`. The parser retains untouched fields from its seed curve. Previously that seed was the native relative curve, while the post-processing step always subtracted the exported microtonal/fine-tuning offset. A lyric-only plugin could consequently shift the curve and cancel the intended tuning.

The parser is now seeded with the exported `UstNote` curve, including its physical tuning offset. The existing inverse conversion then runs once after parsing. Untouched points retain their original native-relative values; explicitly returned UST pitch values are converted back into native-relative coordinates. The source note is cloned, so parsing does not mutate the open project's existing curve.

`XenPluginTest` covers lyric/length-only output, partial PBY output and complete PBS/PBW/PBY output for both 53-EDO addresses and legacy integer-cent fine tuning. No external plugin executable or voicebank is required for these parser-boundary cases.

## Verification status

The fixes and regression cases are ready for the parent's serial build/test run. No parallel build or test process was started while making these changes.
