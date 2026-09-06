# OPUS-MT — où on en est

> **État au 6 septembre 2026.** Le modèle est exporté, et le moteur C# qui s'en
> sert **fonctionne** : la chaîne lecture → ordre → traduction rend du français
> sur une vraie planche.

## Ce qu'est OPUS-MT

Un modèle de traduction neuronale entraîné par le groupe Helsinki-NLP sur les
corpus parallèles OPUS, avec le moteur Marian. Un modèle **par paire de langues**
— ici `Helsinki-NLP/opus-mt-en-fr` — ce qui le rend petit et rapide : il tourne
sur processeur, la carte graphique ne sert pas.

C'est aussi ce qui se cache derrière Argos Translate, donc derrière
LibreTranslate.

Sa limite de fond : il traduit **phrase par phrase sans aucune mémoire**. Le
registre, le tutoiement et le genre des pronoms varient donc d'une bulle à
l'autre sur la même page, et ça ne se règle pas — c'est l'architecture. Seul un
LLM, à qui l'on peut donner le dialogue entier, y répondrait.

Sur une entrée très courte ou abîmée, il part aussi parfois en répétition. Le
moteur borne la génération pour couper court, mais le texte rendu restera mauvais.

## Le moteur C#

`TraductionOpusMt` implémente `ITraducteur` et se construit par
`ChoixDuTraducteur`. Mesuré : **1,3 s** de chargement, puis **environ 630 ms par
bulle**. Rien n'utilise la carte graphique.

Deux pièges ont été trouvés en mesurant, et **chacun aurait produit du charabia
sans que rien ne le signale**. Ils sont commentés dans le code avec leurs
chiffres ; ne les défaites pas sans refaire la mesure.

**La tokenisation.** `source.spm` porte 32 000 pièces avec ses propres
identifiants, le modèle en attend 59 514 numérotés autrement — concordance nulle
sur les 200 premiers. On tokenise donc en *pièces* avec SentencePiece, puis on
traduit ces pièces en identifiants par `vocab.json`. Se servir directement des
identifiants de SentencePiece compile, s'exécute, et rend n'importe quoi.

**La casse.** Le modèle n'a jamais vu de texte crié, or l'OCR d'une planche ne
produit que des majuscules. Trois phrases d'essai contre les mêmes en casse
normale : trois contresens sur trois, avec des mots inventés. Le moteur
normalise donc la casse avant de traduire, en remettant le pronom « I » isolé en
majuscule. Un test d'intégration protège cette correction.

### Le décodage

Recherche en faisceau à quatre hypothèses, ce que recommande la configuration du
modèle. Chaque faisceau porte son propre cache de décodeur — deux hypothèses
divergentes n'ont pas le même état interne. Les scores sont convertis en
logarithmes de vraisemblance, puis **rapportés à la longueur** : sans cette
normalisation, une phrase tronquée gagnerait toujours contre une phrase complète.

Mesuré contre un décodage glouton : **629 ms par phrase au lieu de 289**, et le
glouton perdait des adverbes. `new TraductionOpusMt(dossier, 1)` redonne
exactement le glouton si la vitesse compte plus.

### Ce que le contrat ne promet plus

Ni « une zone déjà traduite est épargnée », ni « la planche reçue reste
intacte » : le moteur remplit les zones sur place, comme le fait déjà
l'ordonnanceur. **La première sera à rétablir** le jour où le front permettra de
corriger une traduction — sans elle, relancer l'étape écrase le travail de
l'utilisateur, ce qui contredit la contrainte fondatrice du projet.

## Ce qui est sur la machine

`backend/modeles/opus-mt-en-fr/` — **1,17 Go**, non versionné.

| Fichier | Taille | À quoi ça sert |
|---|---|---|
| `encoder_model.onnx` | 190 Mo | encode la phrase anglaise |
| `decoder_model_merged.onnx` | 330 Mo | décode, avec ou sans cache |
| `decoder_model.onnx` | 330 Mo | redondant avec le fusionné |
| `decoder_with_past_model.onnx` | 318 Mo | redondant avec le fusionné |
| `source.spm` / `target.spm` | 1,5 Mo | tokeniseurs SentencePiece |
| `vocab.json` | 1,4 Mo | correspondance morceaux → identifiants |
| `config.json`, `generation_config.json` | — | paramètres du modèle |

**Seuls `encoder_model.onnx` et `decoder_model_merged.onnx` sont nécessaires** :
le fusionné remplace à lui seul les deux autres décodeurs. Les supprimer ramène
le dossier à ~520 Mo.

## L'environnement Python

Nom : **`.venv-export`**, à la racine du dépôt. Il s'auto-ignore — virtualenv y
dépose son propre `.gitignore`.

Il ne sert **qu'à l'export**, une fois. Le pipeline C# ne dépend d'aucun Python à
l'exécution : il ne lira que des `.onnx`.

Paquets installés : `optimum 2.1.0`, `optimum-onnx 0.1.0`,
`transformers 4.57.6`, `onnxruntime 1.29.0`, `sentencepiece 0.2.2`.

À noter : le C# utilise `Microsoft.ML.OnnxRuntime 1.29.0`, la même version que
l'ONNX Runtime qui a servi à valider l'export.

## Commandes utiles

Toutes depuis la racine du dépôt. On appelle le `python.exe` de l'environnement
directement, sans l'activer : la politique d'exécution de PowerShell bloque
souvent `Activate.ps1`.

**Refaire l'environnement de zéro**

```powershell
python -m pip install --user virtualenv
python -m virtualenv --system-site-packages .venv-export
.\.venv-export\Scripts\python.exe -m pip install "optimum-onnx[onnxruntime]" transformers sentencepiece
```

**Réexporter le modèle**

```powershell
.\.venv-export\Scripts\optimum-cli.exe export onnx `
  --model Helsinki-NLP/opus-mt-en-fr `
  backend\modeles\opus-mt-en-fr\
```

**Vérifier ce qui est en place**

```powershell
Get-ChildItem backend\modeles\opus-mt-en-fr\ |
  Select-Object Name, @{n='Mo';e={[math]::Round($_.Length/1MB,1)}}
```

**Une autre paire de langues** : remplacer `en-fr` dans le nom du modèle et dans
le dossier de sortie.

## Les pièges rencontrés sur cette machine

Trois obstacles, qui reviendront sur toute machine configurée pareil.

**`C:\Python312\Scripts` n'est pas accessible en écriture.** Python est installé
pour tous les utilisateurs, donc y écrire demande les droits administrateur. Tout
`pip install` global échoue sur un `WinError 2 ... .deleteme`. D'où le
`--user` pour virtualenv, et l'environnement virtuel pour le reste.

**Le module `venv` de la bibliothèque standard est absent** — `C:\Python312\Lib\venv`
n'existe pas. C'est anormal ; `virtualenv` le remplace sans rien demander. Une
réparation de l'installation Python le rétablirait, mais ce n'est pas nécessaire.

**`site-packages` contient des résidus `~ip`** d'anciennes mises à jour de pip
interrompues par le problème de droits. Inertes, mais ils rendent chaque appel à
pip bavard. Supprimables sans risque.

## L'avertissement de l'export

L'export s'est terminé sur :

```
max diff = 1.71661376953125e-05 (atol: 1e-05)
```

L'écart entre le modèle PyTorch et sa version ONNX dépasse d'un cheveu la
tolérance par défaut. À cette échelle, c'est du bruit de calcul en virgule
flottante, sans effet sur une traduction. À ne pas confondre avec un vrai
problème d'export si vous relancez la commande et revoyez ce message.