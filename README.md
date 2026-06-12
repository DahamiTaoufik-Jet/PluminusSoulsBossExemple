# PluminusSoulsBossExemple

Un projet d'exemple de combat de boss de type Souls-like créé sur Unity.

## Concept du jeu

Il s'agit d'un jeu de combat 3D de type Souls-like où un joueur affronte un boss. Le système de combat s'articule autour de mécaniques exigeantes :
- **Caméra et ciblage** : Un système de verrouillage (lock-on) permanent centré sur l'adversaire.
- **Le Joueur** : Dispose d'attaques verticales et d'une esquive circulaire dotée de frames d'invulnérabilité (i-frames).
- **Le Boss** : Possède une panoplie d'attaques variées (frappes au corps-à-corps, estoc, attaque sautée de zone) et un bouclier capable de punir le joueur par une contre-attaque inesquivable s'il frappe au mauvais moment.

**Objectif principal (IA) :** 
L'objectif final de ce projet est de servir de base pour intégrer **Pluminus**, un plugin d'apprentissage par renforcement (Q-learning). Le but est de créer un environnement d'entraînement où deux IA s'affrontent (un joueur artificiel contre le boss) afin que le boss apprenne de lui-même à se battre de manière optimale.



https://github.com/user-attachments/assets/d9734afc-8d11-4af5-a733-458d597c64fd



## Pour commencer

### Prérequis

* **Unity Editor `6000.3.9f1`**

### Installation

1. Clonez ce dépôt ou téléchargez les fichiers du projet.
2. Ouvrez **Unity Hub**.
3. Cliquez sur **Ajouter (Add)** et sélectionnez le dossier racine de ce projet (`PluminusSoulsBossExemple`).
4. Ouvrez le projet dans Unity.
5. Ouvrez la scène principale située dans le dossier `Assets` (généralement sous `Assets/Scenes`) pour voir le projet en action.

## Structure du projet

* **Assets/** - Contient tous les assets du jeu, les scripts, les scènes et les prefabs.
* **Packages/** - Contient les packages Unity utilisés dans ce projet.
* **ProjectSettings/** - Contient la configuration et les paramètres du projet Unity.

## Licence

Ce projet est conçu comme un point de départ ou un exemple pour des mécaniques de boss de type Souls-like.
