# Conventions de code C#

Ce dossier ne contient que du C#. Ces règles s'appliquent à **toutes les classes écrites ici** (entités, managers, etc.).

1. **Propriétés classiques, pas d'auto-property** : écrire le `get`/`set` explicitement avec un corps (`get { return champ; }` / `set { champ = value; }`), jamais `{ get; set; }`.
2. **Nommage** : le champ privé (l'attribut) en `camelCase` **minuscule**, la propriété publique en `PascalCase` **majuscule**.
3. **Initialisation dans le constructeur** : déclarer les champs nus (`private string name;`) et les initialiser dans le **constructeur**, jamais à la déclaration.
4. **Documentation XML** : documenter avec `/// <summary>` **uniquement les membres `public`** (classe, constructeur, propriétés, méthodes) — **pas** les champs `private`, sauf s'il s'agit d'un privé « technique » (non trivial). Pour les méthodes, mettre tout ce qu'il faut : `<summary>`, un `<param>` par paramètre (entrées), `<returns>` (sortie) et un `<exception>` par exception possible. **Toute la documentation (les `summary`) est rédigée en français.**

5. **Régions** : structurer chaque classe avec des `#region`, **dans cet ordre**, en omettant simplement celles qui seraient vides. La liste est **fermée** : pas d'autre nom de région, pas de région inventée pour un cas particulier, et pas de région imbriquée.

| Région | Ce qu'elle contient |
|---|---|
| `Constantes` | les `const` et les `static readonly` |
| `Attributs` | les variables privées de la classe |
| `Constructeurs` | tous les constructeurs |
| `Propriétés` | toutes les propriétés, y compris celles qui sont calculées |
| `Méthodes` | les méthodes publiques |
| `Méthodes privées` | les méthodes d'aide internes |

Une classe qui n'aurait que des champs et des propriétés n'écrit donc que `Attributs`, `Constructeurs` et `Propriétés`.

**Deux exceptions, sans aucune région :**
- les **interfaces**, qui n'ont ni champ ni constructeur ;
- les **classes de test**, qui ne contiennent que des méthodes — il n'y a rien à séparer, et une région unique enveloppant tout le fichier n'apporte que du bruit.

Exemple de référence (illustre les 5 règles) :

```csharp
/// <summary>
/// Représente un artiste.
/// </summary>
public class Artist
{
    #region Attributs

    private string name;

    #endregion

    #region Constructeurs

    /// <summary>
    /// Initialise un nouvel artiste avec des valeurs par défaut.
    /// </summary>
    public Artist()
    {
        name = string.Empty;
    }

    #endregion

    #region Propriétés

    /// <summary>
    /// Nom de l'artiste.
    /// </summary>
    public string Name
    {
        get { return name; }
        set { name = value; }
    }

    #endregion
}
```
