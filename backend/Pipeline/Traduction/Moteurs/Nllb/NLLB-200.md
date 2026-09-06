# NLLB-200 — où on en est

> **État au 6 septembre 2026.** Le modèle est exporté et le moteur C# fonctionne.
> Il traduit mieux qu'OPUS-MT, et **huit fois plus lentement**.

## Ce qu'est NLLB-200

*No Language Left Behind*, un modèle multilingue de Meta couvrant deux cents
langues dans un seul réseau. On emploie la variante distillée à 600 millions de
paramètres, `facebook/nllb-200-distilled-600M`.

Là où OPUS-MT est dédié à une paire de langues, celui-ci les porte toutes — d'où
sa taille et sa lenteur. En échange, il rend un français plus fidèle et **digère
le texte tout en majuscules sans aide**, ce qu'OPUS-MT est incapable de faire.

## Ce qu'il vaut, mesuré

| | OPUS-MT | NLLB-200 |
|---|---|---|
| Chargement | 1,3 s | **7,0 s** |
| Par bulle | 0,63 s | **5,2 s** |
| Poids utile | 520 Mo | **6,9 Go** |
| Vocabulaire | 59 514 | **256 206** |

Sur une planche de sept blocs : **36 s contre 4,4 s**. L'écart vient du
vocabulaire, quatre fois plus grand, qu'il faut parcourir à chaque tour et pour
chaque faisceau.

Sur `WELL, ONE OF OUR INSTRUCTORS SUDDENLY QUIT.` il restitue le « Eh bien »
qu'OPUS-MT laisse tomber, sans qu'on ait rien fait pour l'y aider.

## Le moteur C#

`TraductionNllb` hérite de `TraductionSeq2SeqOnnx`, qui porte la recherche en
faisceau et la gestion du cache. Il ne déclare que ce qui lui est propre — et
**trois choses le distinguent d'OPUS-MT**, toutes mesurées sur les fichiers
réels avant d'écrire une ligne.

**Les identifiants.** Ceux du modèle valent exactement ceux de SentencePiece
**plus un** — vérifié sur 19 000 pièces, écart unique, sans une seule exception.
C'est l'héritage de fairseq, qui range ses quatre jetons spéciaux en tête et
décale tout le reste. Les employer tels quels décalerait tout le texte d'un
jeton : du charabia que rien ne signalerait.

**Les langues.** La langue source s'annonce en tête de l'entrée
(`eng_Latn` = 256047), et la langue cible doit être **imposée comme premier jeton
produit** (`fra_Latn` = 256057). Sans ça, le modèle traduirait vers n'importe
laquelle des deux cents langues qu'il connaît. Ces codes ne sont pas dans le
modèle SentencePiece : ils s'ajoutent après lui, et leur rang se lit dans
`tokenizer.json`.

**Deux décodeurs séparés**, et non un seul piloté par un drapeau comme OPUS-MT.
Le second ne reçoit pas l'état de l'encodeur — il le retrouve dans le cache
d'attention croisée — et ne rend que le cache du décodeur.

### Pas de normalisation de casse

Volontairement. C'est une correction écrite pour OPUS-MT, mesurée pour lui.
NLLB s'en sort seul, et l'ajouter ici serait du bruit.

## Ce qui est sur la machine

`E:\ScanTrad-modeles\nllb-200-distilled-600M` — **6,9 Go**, non versionné.

**Hors du dépôt, et ce n'est pas un choix esthétique** : il ne restait que 3,2 Go
libres sur `C:`. Le chemin est écrit en dur dans `ChoixDuTraducteur`, ce qui lie
le projet à cette machine — provisoire, à remplacer par de la configuration.

| Fichier | Taille |
|---|---|
| `encoder_model.onnx` | 1,6 Go |
| `decoder_model.onnx` + son `.onnx_data` | 2,8 Go |
| `decoder_with_past_model.onnx` + son `.onnx_data` | 2,7 Go |
| `sentencepiece.bpe.model` | 4,6 Mo |
| `tokenizer.json` | 31 Mo |

Les décodeurs dépassent la limite de deux gigaoctets du format protobuf : leurs
poids vivent donc dans un fichier `.onnx_data` à côté. ONNX Runtime les charge
tout seul **à condition qu'ils restent dans le même dossier**.

## L'export

Même environnement que pour OPUS-MT — voir son mémo pour le remonter. On redirige
le cache Hugging Face vers `E:` pour ne rien écrire sur le disque système.

```powershell
$env:HF_HOME = "E:\ScanTrad-modeles\hf-cache"
.\.venv-export\Scripts\optimum-cli.exe export onnx `
  --model facebook/nllb-200-distilled-600M `
  E:\ScanTrad-modeles\nllb-200-distilled-600M\
```

### L'export « échoue », et ce n'est pas grave

La commande se termine sur :

```
RuntimeError: The post-processing of the ONNX export failed.
The export can still be performed by passing the option --no-post-process
```

**Tout ce dont on a besoin est pourtant écrit.** Seule la fusion des deux
décodeurs en un `decoder_model_merged.onnx` a échoué — vraisemblablement parce
que le résultat dépasserait les deux gigaoctets. Or on emploie les deux décodeurs
séparés, donc ce fichier ne sert à rien ici.

Ne relancez pas l'export en croyant qu'il a raté : vérifiez d'abord que les cinq
fichiers du tableau ci-dessus sont là.

## Quand employer lequel

**OPUS-MT** pour itérer : huit fois plus rapide, et la traduction n'est qu'un
brouillon que l'utilisateur corrigera.

**NLLB-200** quand la qualité prime sur le temps — un rendu final, ou une planche
dont la traduction automatique compte vraiment.

Le choix se fait par `MoteurDeTraduction`, et le pipeline ne voit aucune
différence entre les deux.
