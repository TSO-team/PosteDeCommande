# PosteDeCommande

Pour pouvoir s'intégrer au système, le poste de commande devra pouvoir au minimum :
- Établir un lien de communication fiable et sécuritaire entre son ordinateur et son Beaglebone Blue en utilisant un protocole qui encrypte les données pour les protéger en tenant compte du fait que l’accès au système pourrait se faire à distance par Internet ;
- Reconnaître et récupérer les messages CAN qui lui seront d'intérêts ;
- Émettre des messages qui lui permettront d'indiquer si tous les éléments du système doivent être en arrêt ou en opération ;
- Recevoir et interpréter tous les messages CAN qui seront émis par les autres éléments du système.
