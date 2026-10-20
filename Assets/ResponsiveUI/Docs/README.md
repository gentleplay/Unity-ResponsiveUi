# Responsive UI — Documentation

Every document in the package, grouped by what you are trying to do. If you only read two, read
[CONCEPTS.md](CONCEPTS.md) then [GETTING-STARTED.md](GETTING-STARTED.md).

## Use it

| Doc | Question it answers |
|-----|---------------------|
| [GETTING-STARTED.md](GETTING-STARTED.md) | How do I go from an empty project to one responsive screen I can preview? |
| [COMPONENTS.md](COMPONENTS.md) | What does each runtime component do, what is in its Inspector, and what can I call from code? |
| [SETTINGS.md](SETTINGS.md) | What is on **Project Settings → Responsive UI**, including the Android Fold, Android Flip and iPhone Duo pose pages? |

### Module pages

| Doc | Question it answers |
|-----|---------------------|
| [../FoldAndFlip/README.md](../FoldAndFlip/README.md) | What does the Android fold / flip module add — menus, device profiles, posture contract? |
| [../IphoneDuo/README.md](../IphoneDuo/README.md) | What does the iPhone Duo module add — postures, holds, rules, runtime API, diagnostics? |
| [../PhonePad/README.md](../PhonePad/README.md) | How do I edit the phone / pad catalog and generate Simulator skins? |

## Understand it

| Doc | Question it answers |
|-----|---------------------|
| [CONCEPTS.md](CONCEPTS.md) | What are form factors, slots, the fallback chain, the design canvas and design scale, and who applies what? |
| [PLATFORMS.md](PLATFORMS.md) | Which forms can each platform report, and how do I add an adapter for another hinged device? |
| [../FoldAndFlip/Docs/DETECTION.md](../FoldAndFlip/Docs/DETECTION.md) | What do Android and iOS actually expose about a hinge, and how does the detection pipeline consume it? |

## Ship it

| Doc | Question it answers |
|-----|---------------------|
| [PRODUCTION.md](PRODUCTION.md) | What ships in the package, what is excluded, and what is the release checklist? |
| [DEVICE-TESTING.md](DEVICE-TESTING.md) | How do I verify a build on real hardware and on the iPhone Duo simulator? |
| [MIGRATION.md](MIGRATION.md) | How do I upgrade from an older version or the standalone fold and flip kit? |

## Contribute

| Doc | Question it answers |
|-----|---------------------|
| [STRUCTURE.md](STRUCTURE.md) | How is the package laid out — folders, assemblies, namespaces, dependency rules, Simulator state flow, C# conventions? |
| [TESTING.md](TESTING.md) | How do I run the EditMode suites and the PowerShell validators, and what does each one guard? |

Install, requirements and the feature overview live in the package entry point:
**[../README.md](../README.md)**.
