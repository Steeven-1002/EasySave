Groupe 1 :
Antonin CREVON
Steeven BIHEL
Virgile VILLARD
Grégoire DUPONT

Cahier de Tests

Sommaire
Introduction

Introduction

Un cahier de tests a pour objectif de montrer toutes les possibilités d'utilisation d'une application. Ici, chaque option est décrite afin de faire une rétrospection de toutes les fonctionalités. Notre application EasySave permet de créer des sauvegardes de vos fichiers de manière simple, efficaces et intuitives. Retrouvez dans ce document les réponses à vos questions concernant l'utilisation de EasySave.

Tests unitaires

| ID Test | Titre du Test | Étapes | Résultats Attendus (basés sur lang_fr.json) |
|---|---|---|---|
| UI-FR-001 | Affichage du message de bienvenue et menu principal | 1. Lancer l'application.<br>2. Si l'invite de langue apparaît, choisir ""Français"".<br>3. Observer le message de bienvenue et le menu principal. | 1. Le message ""Bienvenue dans EasySave !"" s'affiche.<br>2. Le titre ""--- Menu Principal EasySave ---"" s'affiche.<br>3. Les options du menu s'affichent correctement en français : ""Créer un nouveau travail de sauvegarde"", ""Lister les travaux de sauvegarde"", ""Exécuter un travail de sauvegarde spécifique"" (ou similaire basé sur <br>""Exécuter un travail de sauvegarde""), ""Exécuter plusieurs travaux de sauvegarde"", ""Supprimer un travail de sauvegarde"", ""Changer de langue"", ""Quitter le programme"". |
| UI-FR-002 | Changement de langue vers Français | 1.<br> Lancer l'application (supposer qu'elle est en anglais ou invite au choix).<br>2. Naviguer vers l'option ""Changer de langue"".<br> 3. Entrer ""fr"" comme code de langue. | 1. L'application affiche ""Langue changée avec succès en fr."" ou un message formaté ""Langue changée avec succès en Français."" utilisant ""LanguageName_fr"".<br> 2. L'interface utilisateur (menus, invites) passe en français. |
| UI-FR-003 | Invites de saisie en Français (Création de tâche) | 1.<br> Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Observer les invites de saisie. | Les invites suivantes s'affichent en français : ""Entrez le nom du travail"", ""Entrez le chemin du répertoire source"", ""Entrez le chemin du répertoire cible"", ""Entrez le type de sauvegarde (FULL/DIFFERENTIAL)"". |
| UI-FR-004 | Messages de confirmation en Français (Suppression) | 1.<br> Créer un travail de sauvegarde.<br>2. Choisir ""Supprimer un travail de sauvegarde"".<br>3. Sélectionner le travail à supprimer.<br> 4. Confirmer la suppression (""oui""). | 1. L'invite de confirmation ""Êtes-vous sûr de vouloir supprimer le travail '{0}' ?<br> (oui/non) : "" s'affiche.<br>2. Après confirmation, le message ""Travail '{0}' supprimé avec succès."" s'affiche. |
| UI-FR-005 | Affichage de la liste des travaux (vide et non vide) | 1.<br> Choisir ""Lister les travaux de sauvegarde"" quand aucun travail n'existe.<br>2. Créer un travail.<br> 3. Choisir ""Lister les travaux de sauvegarde"". | 1. Le message ""Aucun travail de sauvegarde n'est actuellement configuré."" s'affiche.<br> 2. Le titre ""--- Travaux de Sauvegarde Configurés ---"" s'affiche, suivi des détails du travail.<br> L'état du travail doit correspondre aux statuts définis (ex: ""Inactif""). |
| UI-FR-006 | Message de sortie de l'application | 1.<br> Choisir ""Quitter le programme"" depuis le menu principal. | Le message ""Fermeture d'EasySave.<br> Au revoir !"" s'affiche avant la fermeture. |
| UI-FR-007 | ""Invite ""Appuyez sur Entrée pour continuer"" | 1. Exécuter une action qui affiche un résultat (ex: lister les travaux). | L'invite ""Appuyez sur Entrée pour continuer..."" s'affiche. |
