# Conventions du front Angular

Le front vit dans `frontend/`, à côté de `backend/`. Ces règles s'appliquent à **tout le code écrit dans `frontend/src/app/`**.

## Structure des dossiers

L'application est **organisée par fonctionnalité** : une partie du site = un dossier dans `features/`.

```
frontend/
├── proxy.conf.json              → /api  ➜  http://localhost:5138 (l'API en développement)
└── src/
    ├── environments/            → configuration par environnement
    └── app/
        ├── app.config.ts        → configuration globale (HttpClient, router…)
        ├── app.routes.ts        → une route par feature, chargée à la demande
        │
        ├── core/                → chargé une seule fois, utilisé partout
        │   ├── api/             → client HTTP, lecture des erreurs ProblemDetails
        │   └── layout/          → coquille du site : en-tête, navigation
        │
        ├── shared/              → briques réutilisables, sans logique métier
        │   └── ui/              → carte de manga, rubrique défilante, loader…
        │
        └── features/            → une feature = une partie du site = un dossier
            ├── accueil/         → rubriques, bibliothèque, recherche   (epic E5)
            ├── manga/           → fiche d'un manga et ses tomes         (epic E5)
            ├── lecteur/         → lecture, bascule VO/VF                (epic E6)
            ├── depot/           → déposer un manga ou un tome           (epic E4)
            ├── traduction/      → lancer et suivre une traduction      (epic E7)
            ├── compte/          → inscription, connexion                (epic E3)
            └── admin/           → administration                        (epic E8)
```

Chaque feature correspond à une epic du backlog : le code d'une US se range dans la feature de son epic.

## Structure d'une feature

Toutes les features suivent le même découpage, en omettant simplement les dossiers qui seraient vides :

```
features/compte/
├── compte.routes.ts     → les routes de la feature (/compte/inscription…)
├── pages/               → les écrans complets, reliés à une route
├── components/          → les morceaux d'écran propres à cette feature
├── services/            → les appels à l'API de cette feature
└── models/              → les types (RegisterRequest, ProblemDetails…)
```

| Dossier | Ce qu'il contient |
|---|---|
| `pages/` | un composant par écran, déclaré dans `<feature>.routes.ts` |
| `components/` | des composants utilisés uniquement par les pages de cette feature |
| `services/` | les appels HTTP de cette feature, et rien d'autre |
| `models/` | les interfaces et types TypeScript échangés avec l'API |

## Règles de dépendance

1. **Une feature ne dépend jamais d'une autre feature.** Si deux features ont besoin de la même chose, cette chose remonte dans `shared/` (affichage) ou `core/` (logique unique).
2. **`core/` contient ce qui n'existe qu'une fois** dans l'application : le client HTTP, la lecture des erreurs, l'en-tête du site, plus tard l'authentification.
3. **`shared/` contient des briques d'affichage réutilisables**, sans appel à l'API ni logique métier : elles reçoivent leurs données en entrée et émettent des événements.
4. **Chaque feature est chargée à la demande** (`loadChildren` dans `app.routes.ts`) : le code du lecteur n'est pas téléchargé tant qu'on n'ouvre pas le lecteur.

Sens autorisé des dépendances :

```
features/*  ──►  shared/  et  core/
shared/     ──►  (rien d'autre dans app/)
core/       ──►  (rien d'autre dans app/)
```

## Communication avec l'API

- En développement, le front appelle toujours des URL relatives en `/api/…` ; `proxy.conf.json` les redirige vers l'API (`http://localhost:5138`). Pas d'URL d'API écrite en dur dans le code, pas de configuration CORS nécessaire.
- Les erreurs de l'API arrivent au format `ProblemDetails`, avec un champ `code` stable (ex. `User.UsernameTaken`). Le front s'appuie sur ce `code`, jamais sur le texte du message, pour décider quoi afficher.

## À décider

Ces choix ne sont pas encore arrêtés ; les inscrire ici une fois tranchés.

- **Langue des noms de features** : en français ci-dessus (`accueil`, `lecteur`, `depot`), comme le backlog et le pipeline ; l'API, elle, est nommée en anglais.
- **Feuilles de style** : CSS ou SCSS.
- **Bibliothèque de composants** : Angular Material ou aucune.
