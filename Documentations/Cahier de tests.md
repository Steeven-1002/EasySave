Groupe 1 :
Antonin CREVON
Steeven BIHEL
Virgile VILLARD
Grégoire DUPONT

# Cahier de Tests

# Sommaire
Introduction

# Introduction

Un cahier de tests a pour objectif de montrer toutes les possibilités d'utilisation d'une application. Ici, chaque option est décrite afin de faire une rétrospection de toutes les fonctionalités. Notre application EasySave permet de créer des sauvegardes de vos fichiers de manière simple, efficaces et intuitives. Retrouvez dans ce document les réponses à vos questions concernant l'utilisation de EasySave.

Tests unitaires

# 1. Tests de l'Interface Utilisateur et de la Localisation

| ID Test | Titre du Test | Étapes | Résultats Attendus |
|---|---|---|---|
| UI-FR-001 | Affichage du message de bienvenue et menu principal | 1. Lancer l'application.<br>2. Si l'invite de langue apparaît, choisir ""Français"".<br>3. Observer le message de bienvenue et le menu principal. | 1. Le message ""Bienvenue dans EasySave !"" s'affiche.<br>2. Le titre ""--- Menu Principal EasySave ---"" s'affiche.<br>3. Les options du menu s'affichent correctement en français : ""Créer un nouveau travail de sauvegarde"", ""Lister les travaux de sauvegarde"", ""Exécuter un travail de sauvegarde spécifique"" (ou similaire basé sur <br>""Exécuter un travail de sauvegarde""), ""Exécuter plusieurs travaux de sauvegarde"", ""Supprimer un travail de sauvegarde"", ""Changer de langue"", ""Quitter le programme"". |
| UI-FR-002 | Changement de langue vers Français | 1.<br> Lancer l'application (supposer qu'elle est en anglais ou invite au choix).<br>2. Naviguer vers l'option ""Changer de langue"".<br> 3. Entrer ""fr"" comme code de langue. | 1. L'application affiche ""Langue changée avec succès en fr."" ou un message formaté ""Langue changée avec succès en Français."" utilisant ""LanguageName_fr"".<br> 2. L'interface utilisateur (menus, invites) passe en français. |
| UI-FR-003 | Invites de saisie en Français (Création de tâche) | 1.<br> Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Observer les invites de saisie. | Les invites suivantes s'affichent en français : ""Entrez le nom du travail"", ""Entrez le chemin du répertoire source"", ""Entrez le chemin du répertoire cible"", ""Entrez le type de sauvegarde (FULL/DIFFERENTIAL)"". |
| UI-FR-004 | Messages de confirmation en Français (Suppression) | 1.<br> Créer un travail de sauvegarde.<br>2. Choisir ""Supprimer un travail de sauvegarde"".<br>3. Sélectionner le travail à supprimer.<br> 4. Confirmer la suppression (""oui""). | 1. L'invite de confirmation ""Êtes-vous sûr de vouloir supprimer le travail '{0}' ?<br> (oui/non) : "" s'affiche.<br>2. Après confirmation, le message ""Travail '{0}' supprimé avec succès."" s'affiche. |
| UI-FR-005 | Affichage de la liste des travaux (vide et non vide) | 1.<br> Choisir ""Lister les travaux de sauvegarde"" quand aucun travail n'existe.<br>2. Créer un travail.<br> 3. Choisir ""Lister les travaux de sauvegarde"". | 1. Le message ""Aucun travail de sauvegarde n'est actuellement configuré."" s'affiche.<br> 2. Le titre ""--- Travaux de Sauvegarde Configurés ---"" s'affiche, suivi des détails du travail.<br> L'état du travail doit correspondre aux statuts définis (ex: ""Inactif""). |
| UI-FR-006 | Message de sortie de l'application | 1.<br> Choisir ""Quitter le programme"" depuis le menu principal. | Le message ""Fermeture d'EasySave.<br> Au revoir !"" s'affiche avant la fermeture. |
| UI-FR-007 | ""Invite ""Appuyez sur Entrée pour continuer"" | 1. Exécuter une action qui affiche un résultat (ex: lister les travaux). | L'invite ""Appuyez sur Entrée pour continuer..."" s'affiche. |

# 2. Tests de Fonctionnalités de Base des Travaux de Sauvegarde

| ID Test | Titre du Test | Étapes | Résultats Attendus |
|---|---|---|---|
| JB-FR-001 | Création d'un travail de sauvegarde (FULL) | 1. Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Nom : ""TestFullFR1"".<br>3. Source : Chemin valide.<br>4. Cible : Chemin valide.<br>5. Type : ""FULL"". | 1. Le message ""Travail 'TestFullFR1' créé avec succès."" s'affiche.<br>2. Le travail est listé avec le type et les chemins corrects, et l'état ""Inactif"". |
| JB-FR-002 | Création d'un travail de sauvegarde (DIFFERENTIAL) | 1. Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Nom : ""TestDiffFR1"".<br>3. Source : Chemin valide.<br>4. Cible : Chemin valide.<br>5. Type : ""DIFFERENTIAL"". | 1. Le message ""Travail 'TestDiffFR1' créé avec succès."" s'affiche.<br>2. Le travail est listé avec le type et les chemins corrects, et l'état ""Inactif"". |
| JB-FR-003 | Suppression d'un travail de sauvegarde existant | 1. Créer un travail ""ToDeleteFR"".<br>2. Choisir ""Supprimer un travail de sauvegarde"".<br>3. Entrer l'index/nom de ""ToDeleteFR"".<br>4. Confirmer avec ""oui"". | 1. L'invite ""Êtes-vous sûr de vouloir supprimer le travail 'ToDeleteFR' ? (oui/non) : "" s'affiche.<br>2. Le message ""Travail 'ToDeleteFR' supprimé avec succès."" s'affiche.<br>3. ""ToDeleteFR"" n'est plus listé. |
| JB-FR-004 | Annulation de la suppression d'un travail | 1. Créer un travail ""NotDeletedFR"".<br>2. Choisir ""Supprimer un travail de sauvegarde"".<br>3. Entrer l'index/nom de ""NotDeletedFR"".<br>4. Répondre ""non"" à la confirmation. | 1. Le message ""Suppression annulée."" s'affiche.<br>2. ""NotDeletedFR"" est toujours listé. |
| JB-FR-005 | Listage des travaux de sauvegarde | 1. Créer plusieurs travaux (ex: JobA, JobB).<br>2. Choisir ""Lister les travaux de sauvegarde"". | 1. Le titre ""--- Travaux de Sauvegarde Configurés ---"" s'affiche.<br>2. JobA et JobB sont listés avec leurs détails (Nom, Source, Cible, Type, État). |

# 3. Tests d'Exécution des Travaux de Sauvegarde

| ID Test | Titre du Test | Étapes | Résultats Attendus |
|---|---|---|---|
| EX-FR-001 | Exécution d'un travail de sauvegarde FULL (unique) | 1. Créer un travail FULL ""FullExecFR"" avec un répertoire source contenant des fichiers.<br>2. Choisir ""Exécuter un travail de sauvegarde spécifique"" (ou ""Exécuter un travail de sauvegarde"").<br>3. Entrer l'index/nom de ""FullExecFR"". | 1. L'invite ""Entrez le numéro du travail à exécuter"" ou ""Entrez le nom du travail à exécuter"" s'affiche.<br>2. Pendant l'exécution, l'état du travail passe à ""Actif (Analyse des fichiers)"" puis ""Actif (Copie en cours)"".<br>3. Les fichiers sont copiés dans le répertoire cible.<br>4. Après exécution, l'état du travail devient ""Terminé avec succès"". L'heure de <br>dernière exécution est mise à jour. |
| EX-FR-002 | Exécution d'un travail de sauvegarde DIFFERENTIAL | 1. Exécuter une première fois un travail FULL ""SourceDiffFR"".<br>2. Modifier/Ajouter des fichiers dans la source.<br>3. Créer un travail DIFFERENTIAL ""DiffExecFR"" pour la même source/cible.<br>4. Exécuter ""DiffExecFR"". | 1. Seuls les fichiers modifiés/ajoutés depuis la dernière sauvegarde FULL (ou la dernière sauvegarde pour ce job différentiel, selon l'implémentation de LastSuccessfulRunTime) sont copiés.<br>2. Les états ""Actif (Analyse des fichiers)"", ""Actif (Copie en cours)"", et ""Terminé avec succès"" sont observés. |
| EX-FR-003 | Exécution de plusieurs travaux de sauvegarde | 1. Créer deux travaux ""MultiA"" et ""MultiB"".<br>2. Choisir ""Exécuter plusieurs travaux de sauvegarde"".<br>3. Entrer les numéros des travaux ""MultiA"" et ""MultiB"" (ex: ""1;2"" ou ""1-2""). | 1. L'invite ""Entrez les numéros des travaux à exécuter (ex: 1;3 ou 1-3)"" s'affiche.<br>2. Les travaux ""MultiA"" et ""MultiB"" sont exécutés séquentiellement ou en parallèle (selon l'implémentation).<br>3. Les deux travaux se terminent avec l'état ""Terminé avec succès"". |
| EX-FR-004 | Statut du travail après exécution avec erreurs | 1. Créer un travail ""ErrorJobFR"" avec un répertoire source inaccessible après la création.<br>2. Exécuter ""ErrorJobFR"". | 1. L'état du travail passe à ""Terminé avec erreurs"" ou ""Échoué"".<br>2. Des messages d'erreur appropriés sont journalisés (non visibles directement via UI sauf si implémenté). |

# 4. Tests de Gestion des Erreurs et Messages

| ID Test | Titre du Test | Étapes | Résultats Attendus |
|---|---|---|---|
| ERR-FR-001 | Création de travail - Champs manquants | 1. Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Laisser le nom du travail vide et essayer de valider.<br>3. Remplir le nom, mais laisser la source vide et valider. | 1. Le message ""Erreur : Le nom du travail ne peut pas être vide."" s'affiche (si cette vérification spécifique existe).<br>Ou ""Erreur : Nom, source, cible et type sont obligatoires."" s'affiche. |
| ERR-FR-002 | Création de travail - Type de sauvegarde invalide | 1. Choisir ""Créer un nouveau travail de sauvegarde"".<br>2. Remplir tous les champs.<br>3. Entrer ""INVALIDTYPE"" comme type de sauvegarde. | Le message ""Erreur : Type de sauvegarde invalide. Veuillez utiliser FULL ou DIFFERENTIAL."" s'affiche. |
| ERR-FR-003 | Exécution de travail - Index invalide | 1. Choisir ""Exécuter un travail de sauvegarde spécifique"".<br>2. Entrer un numéro de travail qui n'existe pas (ex: 99). | Le message ""Numéro de travail invalide sélectionné."" s'affiche. |
| ERR-FR-004 | Suppression de travail - Index invalide | 1. Choisir ""Supprimer un travail de sauvegarde"".<br>2. Entrer un numéro de travail qui n'existe pas (ex: 99). | Le message ""Numéro de travail invalide sélectionné."" (ou ""Erreur : Travail '{0}' non trouvé."" si la recherche par nom échoue de manière similaire). |
| ERR-FR-005 | Exécution de plusieurs travaux - Entrée invalide | 1. Choisir ""Exécuter plusieurs travaux de sauvegarde"".<br>2. Entrer ""abc"" ou une plage invalide. | Le message ""Aucun travail valide sélectionné pour exécution."" ou un message d'erreur de format s'affiche. |
| ERR-FR-006 | Exécution de plusieurs travaux - Aucun index fourni | 1. Choisir ""Exécuter plusieurs travaux de sauvegarde"".<br>2. Appuyer sur Entrée sans fournir d'index. | Le message ""Aucun numéro de travail fourni pour exécution."" s'affiche. |
| ERR-FR-007 | Changement de langue - Code invalide | 1. Choisir ""Changer de langue"".<br>2. Entrer ""xx"" comme code de langue. | Le message ""Code de langue invalide. Veuillez utiliser 'en' ou 'fr'."" s'affiche. |
| ERR-FR-008 | Changement de langue - Fichier de langue manquant (simulé) | 1. Renommer temporairement lang_de.json en lang_fr.json (si on voulait tester un code ""de"" et que lang_fr.json est le seul qui ne fonctionne pas).<br>2. Choisir ""Changer de langue"".<br>3. Entrer ""fr"". | Le message ""Échec du changement de langue vers fr. Veuillez vérifier que le fichier de langue existe."" s'affiche (ou message similaire de LocalizationService). |
| ERR-FR-009 | Menu Principal - Choix invalide | 1. Au menu principal, entrer une option non listée (ex: 99). | Le message ""Choix invalide. Veuillez réessayer."" s'affiche, suivi de ""Entrez votre choix"". |
