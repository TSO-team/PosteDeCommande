#region les includeslstMessages
using PCANBasicExample.Custom_class;
using System;
using System.IO.Ports;       // Pour utiliser le port série
using System.Text;
using System.Threading;
using System.Windows.Forms;
#endregion

namespace ICDIBasic
{
    public partial class Form1 : Form
    {

        #region CONSTANTES
        const byte VEHICULE = 8;
        const byte ARRET = 1;
        const byte DEMARRE = 0;


        const byte ETAT_VEHICULE = 1;
        const byte ARRETE = 0;
        const byte EN_MARCHE = 1;
        const byte HORS_CIRCUIT = 2;


        const byte VITESSE = 2;

        const byte BATTERIE = 3;

        const byte COULEUR = 4;
        const byte METAL = 0;
        const byte ORANGE = 1;
        const byte NOIR = 2;

        const byte POID = 00;

        const byte NO_STATION = 7;
        const byte STATION_1 = 0;
        const byte STATION_2 = 1;
        const byte STATION_3 = 2;

        const byte HISTO = 192;

        const byte SENS = 8;
        const byte HORAIRE = 0;
        const byte ANTIHORAIRE = 1;

        const byte HORLOGE = 6;

        const byte FESTO_START = 9;
        const byte FESTO = 0;
        string DataOut;
        TrameCan trameCan;
        string receivedata = string.Empty;
        #endregion




        #region Initialisation du port série et de l'histoique
        string[] PortsDisponible = SerialPort.GetPortNames(); //Met la liste des ports série dans un tableau de string

        public Form1()
        {

            InitializeComponent();
            //InitializeBasicComponents();
            //Poltergeist.Text = "";
            // Poltergeist.Text = "0";
            timer3.Start(); // Pour gérer la clock interne du PC, celle pour la synchronisation de l'heure
            COMselector.Items.AddRange(PortsDisponible); // Affiche les ports disponibles UART1
            Connexion.Text = "Connexion";                // Le bouton sert à se connecter UART1
            Connexion.Enabled = false;                   // UART1

            if (COMselector.Items.Count > 0)
            {
                COMselector.SelectedIndex = 0;  // Le port par défaut est le premier port indexé
                BAUDselector.SelectedIndex = 4; // La vitesse par défaut est 9600
                Connexion.Enabled = true; // UART1
            }

            serialPort1.ReadTimeout = 500; // Délais maximum pour les try catch
            serialPort1.WriteTimeout = 500; //Delai maximum pour les try catch
        }
        #endregion




        #region Fermeture du programme lorsque l'utilisateur clique sur X
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen == true)
            {
                serialPort1.Close();
            }
        }
        #endregion

        #region Menu Fichier et compagnie
        #region Radémarrer l'application
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }
        #endregion

        #region Quitter l'applicaition
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion

        #region Menu à propos
        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Alex, Claude, Samuel, John, Philippe", "Hey listen!", MessageBoxButtons.OK);
            if (result1 == DialogResult.Yes)
            {

            }
        }
        #endregion
        #endregion

        #region Actualisation des ports COM disponibles
        private void COMselector_Click(object sender, EventArgs e) // Lorsque l'user clique sur la liste des ports COM
        {

            COMselector.Items.Clear();                             // Flush la liste
            COMselector.Items.AddRange(SerialPort.GetPortNames()); // Relit les ports disponibles
            COMselector.SelectedIndex = 0;                         // Port par défaut = premier port
        }
        #endregion

        #region Connexion et déconnexion au RS232
        private void Connexion_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen == true)           // Si le port série est ouvert
            {
                if (Connexion.Text == "Déconnexion") // Si le programme est déjà connecté au port série
                {


                    Connexion.Text = "Connexion";
                    serialPort1.Close();           // Essaie de te décoonnecter


                }
            }
            else // Lorsque le programme n'est pas connecté au port série et qu'un port série est disponible
            {

                serialPort1.BaudRate = Convert.ToInt32(BAUDselector.Text); //Le baud rate choisi dans la combobox
                serialPort1.Parity = Parity.None;      //Aucune parité
                serialPort1.StopBits = StopBits.One;     //1 stop bit
                serialPort1.DataBits = 8;                //8 data bit
                serialPort1.Handshake = Handshake.None;   //pas de handshake
                serialPort1.PortName = COMselector.Text; //Avec le port série choisi
                serialPort1.Open();                       //Maintenant, connecte-toi 
                Connexion.Text = "Déconnexion";
            }
        }

        public void SendData(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                if (trameCan.ToString() != DataOut)
                {
                    serialPort1.Write(DataOut);
                }
            }

        }


        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(80); //(milliseconds) wait for a certain amount of time to ensure data integrity int len        
            int len = serialPort1.BytesToRead;

            if (len != 0)
            {
                byte[] buff = new byte[len];
                serialPort1.Read(buff, 0, len);
                receivedata = Encoding.Default.GetString(buff);

            }

        }
        #endregion










        //#region Bouton Démarrer le véhicule
        //private void button3_Click(object sender, EventArgs e)
        //{

        //     TxCan2(VEHICULE, DEMARRE);
        //     TxCan2(99, 99);
        //}
        //#endregion

        #region Bouton Arrêter le véhicule

        //private void button2_Click(object sender, EventArgs e)
        //{

        //     TxCan2(VEHICULE, ARRET);
        //     TxCan2(99, 99);
        //}
        //#endregion        



        //#region Lecture du CAN aux 50 ms et prise de décisions
        //private void timer4RealTimeCAN_Tick(object sender, EventArgs e)  // À toutes les 50 ms
        //{
        //    ListViewItem lviCurrentItem;
        //    foreach (MessageStatus msgStatus in m_LastMsgsList)
        //        {
        //            if (msgStatus.MarkedAsUpdated)
        //            { // ATTENTION: CETTE FONCTIONNALITÉ UTILISE UN GHOST LABEL
        //                msgStatus.MarkedAsUpdated = false;
        //                lviCurrentItem = lstMessages.Items[msgStatus.Position];
        //                lviCurrentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
        //                lviCurrentItem.SubItems[3].Text = msgStatus.DataString;
        //                lviCurrentItem.SubItems[4].Text = msgStatus.Count.ToString();
        //                lviCurrentItem.SubItems[5].Text = msgStatus.TimeString;

        //                Poltergeist.Text = lviCurrentItem.SubItems[3].Text; // Met le string lu dans le GHOST LABEL master
        //                PoidsLabel.Text = lviCurrentItem.SubItems[3].Text;
        //                GhostLabelDeRéception.Text = Poltergeist.Text; // Met le string lu dans un GHOST LABEL
        //                RangedTrame.Text = Poltergeist.Text;           // Met le string lu dans un GHOST LABEL
        //                RangedTrame.Text = RangedTrame.Text.Remove(RangedTrame.Text.Length - 7); // Enlève la timestamp et la variation
        //                GhostLabelDeRéception.Text = GhostLabelDeRéception.Text.Remove(GhostLabelDeRéception.Text.Length - 4); // Enlève la valeur variable et la timestamp

        //                string input = Poltergeist.Text;
        //                string sub = input.Substring(3, 2);
        //                Range.Text = sub;
        //            }
        //        }






        ////private void RangedTrame_TextChanged(object sender, EventArgs e)
        ////{
        ////    if (RangedTrame.Text == "02") // Indice de vitesse
        ////    {
        ////        lblSpeed.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
        ////    }

        ////    if (RangedTrame.Text == "03") // Indice de batterie
        ////    {
        ////        lblBattryLevel.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
        ////    }
        ////}

        public void GhostLabelDeRéception_TextChanged(object sender, EventArgs e)
        {
            //if (GhostLabelDeRéception.Text == "01 00") // Lorsque le véhicule est arrêté
            //{
            //    lblEtatVehicule.Text = "Arrêté";
            //}

            //if (GhostLabelDeRéception.Text == "01 01") // Lorsque le véhicule est en marche
            //{
            //    lblEtatVehicule.Text = "En marche";
            //}

            //if (GhostLabelDeRéception.Text == "01 02") // Lorsque le véhicule est hors circuit
            //{
            //    lblEtatVehicule.Text = "En marche";
            //    pictureBox1.Image = PCANBasicExample.Properties.Resources.hm;
            //}




            if (trameCan.mode == 0b010) // Lorsque la station est en marche
            {
                EtatProjet.Text = "ON";
            }

            if (trameCan.mode == 0b000) // Lorsque la station est en arret
            {
                EtatProjet.Text = "OFF";
            }

            if (trameCan.mode == 0b110) // Lorsque la station est en test
            {
                EtatProjet.Text = "TEST";
                BoutonStart.Enabled = false;
                
            }
            else
            {
                BoutonStart.Enabled = false;
            }

            if (trameCan.mode == 0b001) // Lorsque la station est en erreur
            {
                EtatProjet.Text = "ERREUR";
            }

            if (trameCan.color == 0b00) // Lorsque aucun bloc 
            {
                lblBlocColor.Text = "rien";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Rien";
            }

            if (trameCan.color == 0b11) // Lorsque le bloc est métallique
            {
                lblBlocColor.Text = "Métalique";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Metal";
            }

            if (trameCan.color == 0b10) // Lorsque le bloc est noir
            {
                lblBlocColor.Text = "Noir";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
                label32.Text = "Noir";
            }
            if (trameCan.color == 0b01) // Lorsque le bloc est orange
            {
                lblBlocColor.Text = "Orange";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
                label32.Text = "Orange";
            }



            if (trameCan.position == 0b00) // Lorsque le véhicule est à la station de pesée
            {
                lblStation.Text = "Station de pesée";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;

            }

            if (trameCan.position == 0b01) //Le véhicule est à la table FESTO
            {
                lblStation.Text = "Table FESTO";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;
            }
            if (trameCan.position == 0b10) //Le véhicule est à la station de pesage
            {
                lblStation.Text = "Station de pesage";
                // pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;
            }

            if (trameCan.weight == 0b00000000) //Le véhicule est à la station de pesage
            {
                LblPoidBloc.Text = "0";
            }
            else
                {
                  LblPoidBloc.Text = Convert.ToInt32(trameCan.weight).ToString();
                }            


        }



        private void quitterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void redémarrerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }

        private void àProposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Claude, Alex, Samuel, John, Philippe", "Hey listen!", MessageBoxButtons.OK); // Note: À moifier
            if (result1 == DialogResult.Yes)
            {

            }
        }

 

        public void BoutonStart_Click(object sender, EventArgs e)
        {
            if (trameCan.mode == 0b010) // Lorsque la station est en marche
            {
                EtatProjet.Text = "ON";
            }
            else
            {
                trameCan.mode = 0b010;
            }


        }

        private void BoutonStop_Click(object sender, EventArgs e)
        {
            if (trameCan.mode == 0b000) // Lorsque la station est en arret
            {
                EtatProjet.Text = "OFF";
            }
            else
            {
                trameCan.mode = 0b000;
            }
        }

        //private void TrameCan_TextChanged(object sender, EventArgs e)
        //{


        //    if (Poltergeist.Text == "010") // Lorsque le mode est (Marche)
        //    {
        //        ModeLabel.Text = "En marche";
        //    }

        //    if (Poltergeist.Text == "000") // Lorsque le mode est (Arret)
        //    {
        //        ModeLabel.Text = "Arrêt";
        //    }

        //    if (Poltergeist.Text == "100") // Lorsque le mode est (Erreur)
        //    {
        //        ModeLabel.Text = "ERREUR";
        //    }

        //    if (Poltergeist.Text == "001") // Lorsque le mode est (Attente)
        //    {
        //        ModeLabel.Text = "En Attente";
        //    }


        //    if (Poltergeist.Text == "11") // Lorsque le bloc est métallique
        //    {
        //        lblBlocColor.Text = "Métalique";
        //        pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
        //        label32.Text = "Metal";
        //    }

        //    if (Poltergeist.Text == "10") // Lorsque le bloc est noir
        //    {
        //        lblBlocColor.Text = "Noir";
        //        pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
        //        label32.Text = "Noir";
        //    }

        //    if (Poltergeist.Text == "01") // Lorsque le bloc est orange
        //    {
        //        lblBlocColor.Text = "Orange";
        //        pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
        //        label32.Text = "Orange";
        //    }

        //    if (GhostLabelDeRéception.Text == "00") // Lorsque le véhicule est à la station de pesée
        //    {
        //        lblStation.Text = "Station de pesée";
        //        pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;

        //    }

        //    if (GhostLabelDeRéception.Text == "01") //Le véhicule est à la table FESTO
        //    {
        //        lblStation.Text = "Table FESTO";
        //        pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;

        //    }


        //    if (Poltergeist.Text == "010") // Lorsque le mode est (Marche)
        //    {
        //        ModeLabel.Text = "En marche";
        //    }

        //}



    }
}

#endregion
