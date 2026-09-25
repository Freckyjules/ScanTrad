# Consignes du projet

## Langue
- **Toujours me répondre en français.**

## Conventions de code C#
Les conventions de code C# sont décrites dans [backend/CLAUDE.md](backend/CLAUDE.md)

## Conventions du front Angular
La structure et les conventions du front sont décrites dans [front/CLAUDE.md](front/CLAUDE.md)

## Convention de commits (Git)
Utiliser **Conventional Commits** pour tous les messages de commit : `type(portée): description`.

- **Types** : `feat` (nouvelle fonctionnalité), `fix` (correction de bug), `docs` (documentation), `test` (ajout/modif de tests), `refactor` (refactorisation sans changement de comportement), `chore` (config, build, `.gitignore`…), `style` (formatage).
- **Portée** : la zone touchée, ex. `Front`, `API`, `autre a préciser`.
- **Langue** : le **type reste en anglais** (mot-clé de la convention : `feat`, `fix`, `docs`…) ; seule la **description après les deux-points est en français**, courte et à l'impératif.
- **Ne pas** ajouter de mention « Co-Authored-By: Claude » ni d'attribution d'assistant IA dans les commits.

Exemples :
- `feat(entities): ajout des classes Artist, Album, Track`
- `test: intégration des tests unitaires fournis`
- `docs(entities): documentation XML des classes`
- `chore: initialisation de la solution BlindTest`
