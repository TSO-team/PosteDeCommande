#region les includeslstMessages
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;        //Pour mettre des animations
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO.Ports;       // Pour utiliser le port série
using System.IO;             // Pour ouvrir un fichier
using System.Text.RegularExpressions;
using Peak.Can.Basic;
using TPCANHandle = System.Byte;
using System.Net.NetworkInformation;
#endregion

namespace ICDIBasic
{
    public partial class Form1 : Form
    {
       
        #region CONSTANTES
        const byte VEHICULE      = 8;
          const byte ARRET       = 1;
          const byte DEMARRE     = 0;


        const byte ETAT_VEHICULE   = 1;
          const byte ARRETE        = 0;
          const byte EN_MARCHE     = 1;
          const byte HORS_CIRCUIT  = 2;


        const byte VITESSE      = 2;

        const byte BATTERIE     = 3;

        const byte COULEUR      = 4;
          const byte METAL      = 0;
          const byte ORANGE     = 1;
          const byte NOIR       = 2;

        const byte POID         = 00;

        const byte NO_STATION   = 7;
          const byte STATION_1  = 0;
          const byte STATION_2  = 1;
          const byte STATION_3  = 2;

        const byte HISTO        = 192;

        const byte SENS           = 8;
           const byte HORAIRE     = 0;
           const byte ANTIHORAIRE = 1;

        const byte HORLOGE      = 6;

        const byte FESTO_START = 9;
        const byte FESTO = 0;
        #endregion

        #region Unmodified RAW CAN STUFF
        #region Structures

        private class MessageStatus
        {
            private TPCANMsg m_Msg;
            private TPCANTimestamp m_TimeStamp;
            private TPCANTimestamp m_oldTimeStamp;
            private int m_iIndex;
            private int m_Count;
            private bool m_bShowPeriod;
            private bool m_bWasChanged;

            public MessageStatus(TPCANMsg canMsg, TPCANTimestamp canTimestamp, int listIndex)
            {
                m_Msg = canMsg;
                m_TimeStamp = canTimestamp;
                m_oldTimeStamp = canTimestamp;
                m_iIndex = listIndex;
                m_Count = 1;
                m_bShowPeriod = true;
                m_bWasChanged = false;
            }

            public void Update(TPCANMsg canMsg, TPCANTimestamp canTimestamp)
            {
                m_Msg = canMsg;
                m_oldTimeStamp = m_TimeStamp;
                m_TimeStamp = canTimestamp;
                m_bWasChanged = true;
                m_Count += 1;
            }

            public TPCANMsg CANMsg
            {
                get { return m_Msg; }
            }

            public TPCANTimestamp Timestamp
            {
                get { return m_TimeStamp; }
            }

            public int Position
            {
                get { return m_iIndex; }
            }

            public string TypeString
            {
                get { return GetMsgTypeString(); }
            }

            public string IdString
            {
                get { return GetIdString(); }
            }

            public string DataString
            {
                get { return GetDataString(); }
            }

            public int Count
            {
                get { return m_Count; }
            }

            public bool ShowingPeriod
            {
                get { return m_bShowPeriod; }
                set
                {
                    if (m_bShowPeriod ^ value)
                    {
                        m_bShowPeriod = value;
                        m_bWasChanged = true;
                    }
                }
            }

            public bool MarkedAsUpdated
            {
                get { return m_bWasChanged; }
                set { m_bWasChanged = value; }
            }

            public string TimeString
            {
                get { return GetTimeString(); }
            }

            private string GetTimeString()
            {
                double fTime;

                fTime = m_TimeStamp.millis + (m_TimeStamp.micros / 1000.0);
                if (m_bShowPeriod)
                    fTime -= (m_oldTimeStamp.millis + (m_oldTimeStamp.micros / 1000.0));
                return fTime.ToString("F1");
            }

            private string GetDataString()
            {
                string strTemp;

                strTemp = "";

                if ((m_Msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                    return "Remote Request";
                else
                    for (int i = 0; i < m_Msg.LEN; i++)
                        strTemp += string.Format("{0:X2} ", m_Msg.DATA[i]);

                return strTemp;
            }

            private string GetIdString()
            {
                // We format the ID of the message and show it
                //
                if ((m_Msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                    return string.Format("{0:X8}h", m_Msg.ID);
                else
                    return string.Format("{0:X3}h", m_Msg.ID);
            }

            private string GetMsgTypeString()
            {
                string strTemp;

                if ((m_Msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_EXTENDED) == TPCANMessageType.PCAN_MESSAGE_EXTENDED)
                    strTemp = "EXTENDED";
                else
                    strTemp = "STANDARD";

                if ((m_Msg.MSGTYPE & TPCANMessageType.PCAN_MESSAGE_RTR) == TPCANMessageType.PCAN_MESSAGE_RTR)
                    strTemp += "/RTR";

                return strTemp;
            }

        }
        #endregion

        #region Delegates
        /// <summary>
        /// Read-Delegate Handler
        /// </summary>
        private delegate void ReadDelegateHandler();
        private delegate void SetTextBoxReceiveDeleg(string text);
        #endregion

        #region Members
        /// <summary>
        /// Saves the handle of a PCAN hardware
        /// </summary>
        private TPCANHandle m_PcanHandle;
        /// <summary>
        /// Saves the baudrate register for a conenction
        /// </summary>
        private TPCANBaudrate m_Baudrate;
        /// <summary>
        /// Saves the type of a non-plug-and-play hardware
        /// </summary>
        private TPCANType m_HwType;
        /// <summary>
        /// Stores the status of received messages for its display
        /// </summary>
        private System.Collections.ArrayList m_LastMsgsList;
        /// <summary>
        /// Read Delegate for calling the function "ReadMessages"
        /// </summary>
        private ReadDelegateHandler m_ReadDelegate;
        /// <summary>
        /// Receive-Event
        /// </summary>
        private System.Threading.AutoResetEvent m_ReceiveEvent;
        /// <summary>
        /// Thread for message reading (using events)
        /// </summary>
        private System.Threading.Thread m_ReadThread;
        /// <summary>
        /// Handles of the current available PCAN-Hardware
        /// </summary>
        private TPCANHandle[] m_HandlesArray;
        #endregion

        #region Help functions


        private void InitializeBasicComponents()
        {
            // Creates the list for received messages
            //
            m_LastMsgsList = new System.Collections.ArrayList();
            // Creates the delegate used for message reading
            //
            m_ReadDelegate = new ReadDelegateHandler(ReadMessages);
            // Creates the event used for signalize incomming messages 
            //
            m_ReceiveEvent = new System.Threading.AutoResetEvent(false);
            // Creates an array with all possible PCAN-Channels
            //
            m_HandlesArray = new TPCANHandle[] 
            { 
                PCANBasic.PCAN_ISABUS1,
                PCANBasic.PCAN_ISABUS2,
                PCANBasic.PCAN_ISABUS3,
                PCANBasic.PCAN_ISABUS4,
                PCANBasic.PCAN_ISABUS5,
                PCANBasic.PCAN_ISABUS6,
                PCANBasic.PCAN_ISABUS7,
                PCANBasic.PCAN_ISABUS8,
                PCANBasic.PCAN_DNGBUS1,
                PCANBasic.PCAN_PCIBUS1,
                PCANBasic.PCAN_PCIBUS2,
                PCANBasic.PCAN_PCIBUS3,
                PCANBasic.PCAN_PCIBUS4,
                PCANBasic.PCAN_PCIBUS5,
                PCANBasic.PCAN_PCIBUS6,
                PCANBasic.PCAN_PCIBUS7,
                PCANBasic.PCAN_PCIBUS8,
                PCANBasic.PCAN_USBBUS1,
                PCANBasic.PCAN_USBBUS2,
                PCANBasic.PCAN_USBBUS3,
                PCANBasic.PCAN_USBBUS4,
                PCANBasic.PCAN_USBBUS5,
                PCANBasic.PCAN_USBBUS6,
                PCANBasic.PCAN_USBBUS7,
                PCANBasic.PCAN_USBBUS8,
                PCANBasic.PCAN_PCCBUS1,
                PCANBasic.PCAN_PCCBUS2
            };

            // Fills and configures the Data of several comboBox components
            //
            FillComboBoxData();

            // Prepares the PCAN-Basic's debug-Log file
            //
            ConfigureLogFile();
        }

        /// <summary>
        /// COnfigures the Debug-Log file of PCAN-Basic
        /// </summary>
        private void ConfigureLogFile()
        {
            UInt32 iBuffer;

            // Sets the mask to catch all events
            //
            iBuffer = PCANBasic.LOG_FUNCTION_ENTRY | PCANBasic.LOG_FUNCTION_LEAVE | PCANBasic.LOG_FUNCTION_PARAMETERS |
                PCANBasic.LOG_FUNCTION_READ | PCANBasic.LOG_FUNCTION_WRITE;

            // Configures the log file. 
            // NOTE: The Log capability is to be used with the NONEBUS Handle. Other handle than this will 
            // cause the function fail.
            //
            PCANBasic.SetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_LOG_CONFIGURE, ref iBuffer, sizeof(UInt32));
        }

        /// <summary>
        /// Help Function used to get an error as text
        /// </summary>
        /// <param name="error">Error code to be translated</param>
        /// <returns>A text with the translated error</returns>
        private string GetFormatedError(TPCANStatus error)
        {
            StringBuilder strTemp;

            // Creates a buffer big enough for a error-text
            //
            strTemp = new StringBuilder(256);
            // Gets the text using the GetErrorText API function
            // If the function success, the translated error is returned. If it fails,
            // a text describing the current error is returned.
            //
            if (PCANBasic.GetErrorText(error, 0, strTemp) != TPCANStatus.PCAN_ERROR_OK)
                return string.Format("An error occurred. Error-code's text ({0:X}) couldn't be retrieved", error);
            else
                return strTemp.ToString();
        }

        /// <summary>
        /// Includes a new line of text into the information Listview
        /// </summary>
        /// <param name="strMsg">Text to be included</param>
        private void IncludeTextMessage(string strMsg)
        {
            lbxInfo.Items.Add(strMsg);
            lbxInfo.SelectedIndex = lbxInfo.Items.Count - 1;
        }

        /// <summary>
        /// Gets the current status of the PCAN-Basic message filter
        /// </summary>
        /// <param name="status">Buffer to retrieve the filter status</param>
        /// <returns>If calling the function was successfull or not</returns>
        private bool GetFilterStatus(out uint status)
        {
            TPCANStatus stsResult;

            // Tries to get the sttaus of the filter for the current connected hardware
            //
            stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_MESSAGE_FILTER, out status, sizeof(UInt32));

            // If it fails, a error message is shown
            //
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                MessageBox.Show(GetFormatedError(stsResult));
                return false;
            }
            return true;
        }

        /// <summary>
        /// Configures the data of all ComboBox components of the main-form
        /// </summary>
        private void FillComboBoxData()
        {
            // Channels will be check
            //
            btnHwRefresh_Click(this, new EventArgs());

            // Baudrates 
            //
            cbbBaudrates.SelectedIndex = 3; // 250 K

            // Hardware Type for no plugAndplay hardware
            //
            cbbHwType.SelectedIndex = 0;

            // Interrupt for no plugAndplay hardware
            //
            cbbInterrupt.SelectedIndex = 0;

            // IO Port for no plugAndplay hardware
            //
            cbbIO.SelectedIndex = 0;

            // Parameters for GetValue and SetValue function calls
            //
            cbbParameter.SelectedIndex = 0;
        }

        /// <summary>
        /// Activates/deaactivates the different controls of the main-form according
        /// with the current connection status
        /// </summary>
        /// <param name="bConnected">Current status. True if connected, false otherwise</param>
        private void SetConnectionStatus(bool bConnected)
        {
            // Buttons
            //
            btnInit.Enabled = !bConnected;
            btnRead.Enabled = bConnected && rdbManual.Checked;
            btnWrite.Enabled = bConnected;
            btnRelease.Enabled = bConnected;
            btnFilterApply.Enabled = bConnected;
            btnFilterQuery.Enabled = bConnected;
            btnParameterSet.Enabled = bConnected;
            btnParameterGet.Enabled = bConnected;
            btnGetVersions.Enabled = bConnected;
            btnHwRefresh.Enabled = !bConnected;
            btnStatus.Enabled = bConnected;
            btnReset.Enabled = bConnected;

            // ComboBoxs
            //
            cbbBaudrates.Enabled = !bConnected;
            cbbChannel.Enabled = !bConnected;
            cbbHwType.Enabled = !bConnected;
            cbbIO.Enabled = !bConnected;
            cbbInterrupt.Enabled = !bConnected;

            // Hardware configuration and read mode
            //
            if (!bConnected)
                cbbChannel_SelectedIndexChanged(this, new EventArgs());
            else
                rdbTimer_CheckedChanged(this, new EventArgs());

            // Display messages in grid
            //
            tmrDisplay.Enabled = bConnected;
        }

        /// <summary>
        /// Gets the formated text for a CPAN-Basic channel handle
        /// </summary>
        /// <param name="handle">PCAN-Basic Handle to format</param>
        /// <returns>The formatted text for a channel</returns>
        private string FormatChannelName(TPCANHandle handle)
        {
            TPCANDevice devDevice;
            byte byChannel;

            // Gets the owner device and channel for a 
            // PCAN-Basic handle
            //
            devDevice = (TPCANDevice)(handle >> 4);
            byChannel = (byte)(handle & 0xF);

            // Constructs the PCAN-Basic Channel name and return it
            //
            return string.Format("{0} {1} ({2:X2}h)", devDevice, byChannel, handle);
        }
        #endregion

        #region Message-proccessing functions
        /// <summary>
        /// Display CAN messages in the Message-ListView
        /// </summary>
        private void DisplayMessages()
        {
            ListViewItem lviCurrentItem;

            lock (m_LastMsgsList.SyncRoot)
            {
                foreach (MessageStatus msgStatus in m_LastMsgsList)
                {
                    // Get the data to actualize
                    //
                    if (msgStatus.MarkedAsUpdated)
                    {
                        msgStatus.MarkedAsUpdated = false;
                        lviCurrentItem = lstMessages.Items[msgStatus.Position];
                        lviCurrentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
                        lviCurrentItem.SubItems[3].Text = msgStatus.DataString;
                        lviCurrentItem.SubItems[4].Text = msgStatus.Count.ToString();
                        lviCurrentItem.SubItems[5].Text = msgStatus.TimeString;
                    }
                }
            }
        }

        /// <summary>
        /// Inserts a new entry for a new message in the Message-ListView
        /// </summary>
        /// <param name="newMsg">The messasge to be inserted</param>
        /// <param name="timeStamp">The Timesamp of the new message</param>
        private void InsertMsgEntry(TPCANMsg newMsg, TPCANTimestamp timeStamp)
        {
            MessageStatus msgStsCurrentMsg;
            ListViewItem lviCurrentItem;

            lock (m_LastMsgsList.SyncRoot)
            {
                // We add this status in the last message list
                //
                msgStsCurrentMsg = new MessageStatus(newMsg, timeStamp, lstMessages.Items.Count);
                m_LastMsgsList.Add(msgStsCurrentMsg);

                // Add the new ListView Item with the Type of the message
                //	
                lviCurrentItem = lstMessages.Items.Add(msgStsCurrentMsg.TypeString);
                // We set the ID of the message
                //
                lviCurrentItem.SubItems.Add(msgStsCurrentMsg.IdString);
                CANid.Text = Convert.ToString(lviCurrentItem.SubItems.Add(msgStsCurrentMsg.IdString));
                // We set the length of the Message
                //
                lviCurrentItem.SubItems.Add(newMsg.LEN.ToString());
                // We set the data of the message. 	
                //
                lviCurrentItem.SubItems.Add(msgStsCurrentMsg.DataString);
                // we set the message count message (this is the First, so count is 1)            
                //
                lviCurrentItem.SubItems.Add(msgStsCurrentMsg.Count.ToString());
                // Add time stamp information if needed
                //
                lviCurrentItem.SubItems.Add(msgStsCurrentMsg.TimeString);
            }
        }

        /// <summary>
        /// Processes a received message, in order to show it in the Message-ListView
        /// </summary>
        /// <param name="theMsg">The received PCAN-Basic message</param>
        /// <returns>True if the message must be created, false if it must be modified</returns>
        private void ProcessMessage(TPCANMsg theMsg, TPCANTimestamp itsTimeStamp)
        {
            // We search if a message (Same ID and Type) is 
            // already received or if this is a new message
            //
            lock (m_LastMsgsList.SyncRoot)
            {
                foreach (MessageStatus msg in m_LastMsgsList)
                {
                    if ((msg.CANMsg.ID == theMsg.ID) && (msg.CANMsg.MSGTYPE == theMsg.MSGTYPE))
                    {
                        // Modify the message and exit
                        //
                        msg.Update(theMsg, itsTimeStamp);
                        return;
                    }
                }
                // Message not found. It will created
                //
                InsertMsgEntry(theMsg, itsTimeStamp);
            }
        }

        /// <summary>
        /// Thread-Function used for reading PCAN-Basic messages
        /// </summary>
        private void CANReadThreadFunc()
        {
            UInt32 iBuffer;
            TPCANStatus stsResult;

            iBuffer = Convert.ToUInt32(m_ReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
            // Sets the handle of the Receive-Event.
            //
            stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(UInt32));

            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
            {
                MessageBox.Show(GetFormatedError(stsResult), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // While this mode is selected
            while (rdbEvent.Checked)
            {
                // Waiting for Receive-Event
                // 
                if (m_ReceiveEvent.WaitOne(50))
                    // Process Receive-Event using .NET Invoke function
                    // in order to interact with Winforms UI (calling the 
                    // function ReadMessages)
                    // 
                    this.Invoke(m_ReadDelegate);
            }
        }

        /// <summary>
        /// Function for reading PCAN-Basic messages
        /// </summary>
        private void ReadMessages()
        {
            TPCANMsg CANMsg;
            TPCANTimestamp CANTimeStamp;
            TPCANStatus stsResult;

            // We read at least one time the queue looking for messages.
            // If a message is found, we look again trying to find more.
            // If the queue is empty or an error occurr, we get out from
            // the dowhile statement.
            //			
            do
            {
                // We execute the "Read" function of the PCANBasic                
                //
                stsResult = PCANBasic.Read(m_PcanHandle, out CANMsg, out CANTimeStamp);

                // A message was received
                // We process the message(s)
                //
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                    ProcessMessage(CANMsg, CANTimeStamp);

            } while (btnRelease.Enabled && (!Convert.ToBoolean(stsResult & TPCANStatus.PCAN_ERROR_QRCVEMPTY)));
        }
        #endregion
        #endregion

        #region Initialisation du port série et de l'histoique
        string[] PortsDisponible = SerialPort.GetPortNames(); //Met la liste des ports série dans un tableau de string

        public Form1()
        {
            InitializeComponent();
            InitializeBasicComponents();
            //Poltergeist.Text = "";
           // Poltergeist.Text = "0";
            timer3.Start(); // Pour gérer la clock interne du PC, celle pour la synchronisation de l'heure
            COMselector.Items.AddRange(PortsDisponible); // Affiche les ports disponibles UART1
            Connexion.Text = "Connexion";                // Le bouton sert à se connecter UART1
            Connexion.Enabled = false;                   // UART1

            Historique.AppendText("\r\n");
            Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Historique.AppendText("\r\n");
            Historique.AppendText("Programme exécuté");   // Enregistre le log de la connexion

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
            PCANBasic.Uninitialize(m_PcanHandle);
        }
        #endregion

        #region Unmodified RAW CAN StUFF

        #region ComboBox event-handlers (CAN code non modifié)
        private void cbbChannel_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool bNonPnP;
            string strTemp;

            // Get the handle fromt he text being shown
            //
            strTemp = cbbChannel.Text;
            strTemp = strTemp.Substring(strTemp.IndexOf('(') + 1, 2);

            // Determines if the handle belong to a No Plug&Play hardware 
            //
            m_PcanHandle = Convert.ToByte(strTemp, 16);
            bNonPnP = m_PcanHandle <= PCANBasic.PCAN_DNGBUS1;
            // Activates/deactivates configuration controls according with the 
            // kind of hardware
            //
            cbbHwType.Enabled = bNonPnP;
            cbbIO.Enabled = bNonPnP;
            cbbInterrupt.Enabled = bNonPnP;
        }

        private void cbbBaudrates_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Saves the current selected baudrate register code
            //
            switch (cbbBaudrates.SelectedIndex)
            {
                case 0:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_1M;
                    break;
                case 1:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_800K;
                    break;
                case 2:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_500K;
                    break;
                case 3:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_250K;
                    break;
                case 4:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_125K;
                    break;
                case 5:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_100K;
                    break;
                case 6:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_95K;
                    break;
                case 7:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_83K;
                    break;
                case 8:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_50K;
                    break;
                case 9:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_47K;
                    break;
                case 10:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_33K;
                    break;
                case 11:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_20K;
                    break;
                case 12:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_10K;
                    break;
                case 13:
                    m_Baudrate = TPCANBaudrate.PCAN_BAUD_5K;
                    break;
            }
        }

        private void cbbHwType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Saves the current type for a no-Plug&Play hardware
            //
            switch (cbbHwType.SelectedIndex)
            {
                case 0:
                    m_HwType = TPCANType.PCAN_TYPE_ISA;
                    break;
                case 1:
                    m_HwType = TPCANType.PCAN_TYPE_ISA_SJA;
                    break;
                case 2:
                    m_HwType = TPCANType.PCAN_TYPE_ISA_PHYTEC;
                    break;
                case 3:
                    m_HwType = TPCANType.PCAN_TYPE_DNG;
                    break;
                case 4:
                    m_HwType = TPCANType.PCAN_TYPE_DNG_EPP;
                    break;
                case 5:
                    m_HwType = TPCANType.PCAN_TYPE_DNG_SJA;
                    break;
                case 6:
                    m_HwType = TPCANType.PCAN_TYPE_DNG_SJA_EPP;
                    break;
            }
        }

        private void cbbParameter_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Activates/deactivates controls according with the selected 
            // PCAN-Basic parameter 
            //
            rdbParamActive.Enabled = cbbParameter.SelectedIndex != 0;
            rdbParamInactive.Enabled = rdbParamActive.Enabled;
            nudDeviceId.Enabled = !rdbParamActive.Enabled;
        }
        #endregion

        #region Button event-handlers (CAN code non modifié)
        private void btnHwRefresh_Click(object sender, EventArgs e)
        {
            UInt32 iBuffer;
            TPCANStatus stsResult;

            // Clears the Channel combioBox and fill it again with 
            // the PCAN-Basic handles for no-Plug&Play hardware and
            // the detected Plug&Play hardware
            //
            cbbChannel.Items.Clear();
            try
            {
                for (int i = 0; i < m_HandlesArray.Length; i++)
                {
                    // Includes all no-Plug&Play Handles
                    if (m_HandlesArray[i] <= PCANBasic.PCAN_DNGBUS1)
                        cbbChannel.Items.Add(FormatChannelName(m_HandlesArray[i]));
                    else
                    {
                        // Checks for a Plug&Play Handle and, according with the return value, includes it
                        // into the list of available hardware channels.
                        //
                        stsResult = PCANBasic.GetValue(m_HandlesArray[i], TPCANParameter.PCAN_CHANNEL_CONDITION, out iBuffer, sizeof(UInt32));
                        if ((stsResult == TPCANStatus.PCAN_ERROR_OK) && (iBuffer == PCANBasic.PCAN_CHANNEL_AVAILABLE))
                            cbbChannel.Items.Add(FormatChannelName(m_HandlesArray[i]));
                    }
                }
                cbbChannel.SelectedIndex = cbbChannel.Items.Count - 1;
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show("Le fichier PCANBasic.dll est introuvable!", "Missing DLL Err0r!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }
        }

        private void btnInit_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;

            // Connects a selected PCAN-Basic channel
            //
            stsResult = PCANBasic.Initialize(
                m_PcanHandle,
                m_Baudrate,
                m_HwType,
                Convert.ToUInt32(cbbIO.Text, 16),
                Convert.ToUInt16(cbbInterrupt.Text));

            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                MessageBox.Show(GetFormatedError(stsResult));

            // Sets the connection status of the main-form
            //
            SetConnectionStatus(stsResult == TPCANStatus.PCAN_ERROR_OK);
            timer4RealTimeCAN.Start(); // Démarre le timer CAN de réception en temps réel
        }

        private void btnRelease_Click(object sender, EventArgs e)
        {
            // Releases a current connected PCAN-Basic channel
            //
            PCANBasic.Uninitialize(m_PcanHandle);
            tmrRead.Enabled = false;
            if (m_ReadThread != null)
            {
                m_ReadThread.Abort();
                m_ReadThread.Join();
                m_ReadThread = null;
            }

            // Sets the connection status of the main-form
            //
            SetConnectionStatus(false);
        }

        private void btnFilterApply_Click(object sender, EventArgs e)
        {
            UInt32 iBuffer;
            TPCANStatus stsResult;

            // Gets the current status of the message filter
            //
            if (!GetFilterStatus(out iBuffer))
                return;

            // Configures the message filter for a custom range of messages
            //
            if (rdbFilterCustom.Checked)
            {
                // The filter must be first closed in order to customize it
                //
                if (iBuffer != PCANBasic.PCAN_FILTER_OPEN)
                {
                    // Sets the custom filter
                    //
                    stsResult = PCANBasic.FilterMessages(
                    m_PcanHandle,
                    Convert.ToUInt32(nudIdFrom.Value),
                    Convert.ToUInt32(nudIdTo.Value),
                    chbFilterExt.Checked ? TPCANMode.PCAN_MODE_EXTENDED : TPCANMode.PCAN_MODE_STANDARD);
                    // If success, an information message is written, if it is not, an error message is shown
                    //
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The filter was customized. IDs from {0:X} to {1:X} will be received", nudIdFrom.Text, nudIdTo.Text));
                    else
                        MessageBox.Show(GetFormatedError(stsResult));
                }
                else
                    MessageBox.Show("Le filtre doit être fermé si vous voulez le modifier");

                return;
            }

            // The filter will be full opened or complete closed
            //
            if (rdbFilterClose.Checked)
                iBuffer = PCANBasic.PCAN_FILTER_CLOSE;
            else
                iBuffer = PCANBasic.PCAN_FILTER_OPEN;

            // The filter is configured
            //
            stsResult = PCANBasic.SetValue(
                m_PcanHandle,
                TPCANParameter.PCAN_MESSAGE_FILTER,
                ref iBuffer,
                sizeof(UInt32));

            // If success, an information message is written, if it is not, an error message is shown
            //
            if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                IncludeTextMessage(string.Format("The filter a été {0}", rdbFilterClose.Checked ? "closed." : "opened."));
            else
                MessageBox.Show(GetFormatedError(stsResult));
        }

        private void btnFilterQuery_Click(object sender, EventArgs e)
        {
            UInt32 iBuffer;

            // Queries the current status of the message filter
            //
            if (GetFilterStatus(out iBuffer))
            {
                switch (iBuffer)
                {
                    // The filter is closed
                    //
                    case PCANBasic.PCAN_FILTER_CLOSE:
                        IncludeTextMessage("Le filtre est: fermé.");
                        break;
                    // The filter is fully opened
                    //
                    case PCANBasic.PCAN_FILTER_OPEN:
                        IncludeTextMessage("Le filtre est: ouvert.");
                        break;
                    // The filter is customized
                    //
                    case PCANBasic.PCAN_FILTER_CUSTOM:
                        IncludeTextMessage("Le filtre est: personnalisé.");
                        break;
                    // The status of the filter is undefined. (Should never happen)
                    //
                    default:
                        IncludeTextMessage("Le filtre est: invalide.");
                        break;
                }
            }
        }

        private void btnParameterSet_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;
            UInt32 iBuffer;

            // Sets a PCAN-Basic parameter value
            //
            switch (cbbParameter.SelectedIndex)
            {
                // The Device-Number of an USB channel will be set
                //
                case 0:
                    iBuffer = Convert.ToUInt32(nudDeviceId.Value);
                    stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_DEVICE_NUMBER, ref iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage("The desired Device-Number was successfully configured");
                    break;
                // The 5 Volt Power feature of a PC-card or USB will be set
                //
                case 1:
                    iBuffer = (uint)(rdbParamActive.Checked ? PCANBasic.PCAN_PARAMETER_ON : PCANBasic.PCAN_PARAMETER_OFF);
                    stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_5VOLTS_POWER, ref iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The USB/PC-Card 5 power was successfully {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "activated" : "deactivated"));
                    break;
                // The feature for automatic reset on BUS-OFF will be set
                //
                case 2:
                    iBuffer = (uint)(rdbParamActive.Checked ? PCANBasic.PCAN_PARAMETER_ON : PCANBasic.PCAN_PARAMETER_OFF);
                    stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_BUSOFF_AUTORESET, ref iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The automatic-reset on BUS-OFF was successfully {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "activated" : "deactivated"));
                    break;
                // The CAN option "Listen Only" will be set
                //
                case 3:
                    iBuffer = (uint)(rdbParamActive.Checked ? PCANBasic.PCAN_PARAMETER_ON : PCANBasic.PCAN_PARAMETER_OFF);
                    stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_LISTEN_ONLY, ref iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The CAN-option Listen-Only was successfully {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "activated" : "deactivated"));
                    break;
                // The feature for logging debug-information will be set
                //
                case 4:
                    iBuffer = (uint)(rdbParamActive.Checked ? PCANBasic.PCAN_PARAMETER_ON : PCANBasic.PCAN_PARAMETER_OFF);
                    stsResult = PCANBasic.SetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_LOG_STATUS, ref iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The feature for logging debug information was successfully {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "activated" : "deactivated"));
                    break;
                // The current parameter is invalid
                //
                default:
                    stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
                    MessageBox.Show("Wrong parameter code.");
                    return;
            }

            // If the function fail, an error message is shown
            //
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                MessageBox.Show(GetFormatedError(stsResult));
        }

        private void btnParameterGet_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;
            UInt32 iBuffer;

            // Gets a PCAN-Basic parameter value
            //
            switch (cbbParameter.SelectedIndex)
            {
                // The Device-Number of an USB channel will be retrieved
                //
                case 0:
                    stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_DEVICE_NUMBER, out iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The configured Device-Number is {0:X}h", iBuffer));
                    break;
                // The activation status of the 5 Volt Power feature of a PC-card or USB will be retrieved
                //
                case 1:
                    stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_5VOLTS_POWER, out iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The 5-Volt Power of the USB/PC-Card is {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "ON" : "OFF"));
                    break;
                // The activation status of the feature for automatic reset on BUS-OFF will be retrieved
                //
                case 2:
                    stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_BUSOFF_AUTORESET, out iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The automatic-reset on BUS-OFF is {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "ON" : "OFF"));
                    break;
                // The activation status of the CAN option "Listen Only" will be retrieved
                //
                case 3:
                    stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_LISTEN_ONLY, out iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The CAN-option Listen-Only is {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "ON" : "OFF"));
                    break;
                // The activation status for the feature for logging debug-information will be retrieved
                case 4:
                    stsResult = PCANBasic.GetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_LOG_STATUS, out iBuffer, sizeof(UInt32));
                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                        IncludeTextMessage(string.Format("The feature for logging debug information is {0}", (iBuffer == PCANBasic.PCAN_PARAMETER_ON) ? "ON" : "OFF"));
                    break;
                // The current parameter is invalid
                //
                default:
                    stsResult = TPCANStatus.PCAN_ERROR_UNKNOWN;
                    MessageBox.Show("Wrong parameter code.");
                    return;
            }

            // If the function fail, an error message is shown
            //
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                MessageBox.Show(GetFormatedError(stsResult));
        }

        private void btnRead_Click(object sender, EventArgs e)
        {
            TPCANMsg CANMsg;
            TPCANTimestamp CANTimeStamp;
            TPCANStatus stsResult;

            // We execute the "Read" function of the PCANBasic                
            //
            stsResult = PCANBasic.Read(m_PcanHandle, out CANMsg, out CANTimeStamp);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                // We process the received message
                //
                ProcessMessage(CANMsg, CANTimeStamp);
            else
                // If an error occurred, an information message is included
                //
                IncludeTextMessage(GetFormatedError(stsResult));
        }

        private void btnGetVersions_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;
            StringBuilder strTemp;
            string[] strArrayVersion;

            strTemp = new StringBuilder(256);

            // We get the vesion of the PCAN-Basic API
            //
            stsResult = PCANBasic.GetValue(PCANBasic.PCAN_NONEBUS, TPCANParameter.PCAN_API_VERSION, strTemp, 256);
            if (stsResult == TPCANStatus.PCAN_ERROR_OK)
            {
                IncludeTextMessage("API Version: " + strTemp.ToString());
                // We get the driver version of the channel being used
                //
                stsResult = PCANBasic.GetValue(m_PcanHandle, TPCANParameter.PCAN_CHANNEL_VERSION, strTemp, 256);
                if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                {
                    // Because this information contains line control characters (several lines)
                    // we split this also in several entries in the Information List-Box
                    //
                    strArrayVersion = strTemp.ToString().Split(new char[] { '\n' });
                    IncludeTextMessage("Channel/Driver Version: ");
                    for (int i = 0; i < strArrayVersion.Length; i++)
                        IncludeTextMessage("     * " + strArrayVersion[i]);
                }
            }

            // If an error ccurred, a message is shown
            //
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                MessageBox.Show(GetFormatedError(stsResult));
        }

        private void btnMsgClear_Click(object sender, EventArgs e)
        {
            // The information contained in the messages List-View
            // is cleared
            //
            lock (m_LastMsgsList.SyncRoot)
            {
                m_LastMsgsList.Clear();
                lstMessages.Items.Clear();
            }
        }

        private void btnInfoClear_Click(object sender, EventArgs e)
        {
            // The information contained in the Information List-Box 
            // is cleared
            //
            lbxInfo.Items.Clear();
        }

        private void btnWrite_Click(object sender, EventArgs e)
        {
            TPCANMsg CANMsg;
            TextBox txtbCurrentTextBox;
            TPCANStatus stsResult;

            // We create a TPCANMsg message structure 
            //
            CANMsg = new TPCANMsg();
            CANMsg.DATA = new byte[8];

            // We configurate the Message.  The ID (max 0x1FF),
            // Length of the Data, Message Type (Standard in 
            // this example) and die data
            //
            CANMsg.ID = Convert.ToUInt32(txtID.Text, 16);
            CANMsg.LEN = Convert.ToByte(nudLength.Value);
            CANMsg.MSGTYPE = (chbExtended.Checked) ? TPCANMessageType.PCAN_MESSAGE_EXTENDED : TPCANMessageType.PCAN_MESSAGE_STANDARD;
            // If a remote frame will be sent, the data bytes are not important.
            //
            if (chbRemote.Checked)
                CANMsg.MSGTYPE |= TPCANMessageType.PCAN_MESSAGE_RTR;
            else
            {
                // We get so much data as the Len of the message
                //
                txtbCurrentTextBox = txtData0;
                for (int i = 0; i < CANMsg.LEN; i++)
                {
                    CANMsg.DATA[i] = Convert.ToByte(txtbCurrentTextBox.Text, 16);
                    if (i < 7)
                        txtbCurrentTextBox = (TextBox)this.GetNextControl(txtbCurrentTextBox, true);
                }
            }

            // The message is sent to the configured hardware
            //
            stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg);

            // The message was successfully sent
            //
            if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                IncludeTextMessage("Message was successfully SENT");
            // An error occurred.  We show the error.
            //			
            else
                MessageBox.Show(GetFormatedError(stsResult));
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;

            // Resets the receive and transmit queues of a PCAN Channel.
            //
            stsResult = PCANBasic.Reset(m_PcanHandle);

            // If it fails, a error message is shown
            //
            if (stsResult != TPCANStatus.PCAN_ERROR_OK)
                MessageBox.Show(GetFormatedError(stsResult));
            else
                IncludeTextMessage("Receive and transmit queues successfully reset");
        }

        private void btnStatus_Click(object sender, EventArgs e)
        {
            TPCANStatus stsResult;
            String errorName;

            // Gets the current BUS status of a PCAN Channel.
            //
            stsResult = PCANBasic.GetStatus(m_PcanHandle);

            // Switch On Error Name
            //
            switch (stsResult)
            {
                case TPCANStatus.PCAN_ERROR_INITIALIZE:
                    errorName = "PCAN_ERROR_INITIALIZE";
                    break;

                case TPCANStatus.PCAN_ERROR_BUSLIGHT:
                    errorName = "PCAN_ERROR_BUSLIGHT";
                    break;

                case TPCANStatus.PCAN_ERROR_BUSHEAVY:
                    errorName = "PCAN_ERROR_BUSHEAVY";
                    break;

                case TPCANStatus.PCAN_ERROR_BUSOFF:
                    errorName = "PCAN_ERROR_BUSOFF";
                    break;

                case TPCANStatus.PCAN_ERROR_OK:
                    errorName = "PCAN_ERROR_OK";
                    break;

                default:
                    errorName = "See Documentation";
                    break;
            }

            // Display Message
            //
            IncludeTextMessage(String.Format("Status: {0} ({1:X}h)", errorName, stsResult));
        }
        #endregion

        #region Timer event-handler (CAN code non modifié)
        private void tmrRead_Tick(object sender, EventArgs e)
        {
            // Checks if in the receive-queue are currently messages for read
            ReadMessages();
        }

        private void tmrDisplay_Tick(object sender, EventArgs e)
        {
            DisplayMessages();
        }
        #endregion

        #region Message List-View event-handler (CAN code non modifié)
        private void lstMessages_DoubleClick(object sender, EventArgs e)
        {
            // Clears the content of the Message List-View
            //
            btnMsgClear_Click(this, new EventArgs());
        }
        #endregion

        #region Information List-Box event-handler (CAN code non modifié)
        private void lbxInfo_DoubleClick(object sender, EventArgs e)
        {
            // Clears the content of the Information List-Box
            //
            btnInfoClear_Click(this, new EventArgs());
        }
        #endregion

        #region Textbox event handlers (CAN code non modifié)
        private void txtID_Leave(object sender, EventArgs e)
        {
            int iTextLength;
            uint uiMaxValue;

            // Calculates the text length and Maximum ID value according
            // with the Message Type
            //
            iTextLength = (chbExtended.Checked) ? 8 : 3;
            uiMaxValue = (chbExtended.Checked) ? (uint)0x1FFFFFF : (uint)0x7FF;

            // The Textbox for the ID is represented with 3 characters for 
            // Standard and 8 characters for extended messages.
            // Therefore if the Length of the text is smaller than TextLength,  
            // we add "0"
            //
            while (txtID.Text.Length != iTextLength)
                txtID.Text = ("0" + txtID.Text);

            // We check that the ID is not bigger than current maximum value
            //
            if (Convert.ToUInt32(txtID.Text, 16) > uiMaxValue)
                txtID.Text = string.Format("{0:X" + iTextLength.ToString() + "}", uiMaxValue);
        }

        private void txtID_KeyPress(object sender, KeyPressEventArgs e)
        {
            char chCheck;

            // We convert the Character to its Upper case equivalent
            //
            chCheck = char.ToUpper(e.KeyChar);

            // The Key is the Delete (Backspace) Key
            //
            if (chCheck == 8)
                return;
            // The Key is a number between 0-9
            //
            if ((chCheck > 47) && (chCheck < 58))
                return;
            // The Key is a character between A-F
            //
            if ((chCheck > 64) && (chCheck < 71))
                return;

            // Is neither a number nor a character between A(a) and F(f)
            //
            e.Handled = true;
        }

        private void txtData0_Leave(object sender, EventArgs e)
        {
            TextBox txtbCurrentTextbox;

            // all the Textbox Data fields are represented with 2 characters.
            // Therefore if the Length of the text is smaller than 2, we add
            // a "0"
            //
            if (sender.GetType().Name == "TextBox")
            {
                txtbCurrentTextbox = (TextBox)sender;
                while (txtbCurrentTextbox.Text.Length != 2)
                    txtbCurrentTextbox.Text = ("0" + txtbCurrentTextbox.Text);
            }
        }
        #endregion

        #region Radio- and Check- Buttons event-handlers (CAN code non modifié)
        private void chbShowPeriod_CheckedChanged(object sender, EventArgs e)
        {
            // According with the check-value of this checkbox,
            // the recieved time of a messages will be interpreted as 
            // period (time between the two last messages) or as time-stamp
            // (the elapsed time since windows was started)
            //
            lock (m_LastMsgsList.SyncRoot)
            {
                foreach (MessageStatus msg in m_LastMsgsList)
                    msg.ShowingPeriod = chbShowPeriod.Checked;
            }
        }

        private void chbExtended_CheckedChanged(object sender, EventArgs e)
        {
            uint uiTemp;

            txtID.MaxLength = (chbExtended.Checked) ? 8 : 3;

            // the only way that the text length can be bigger als MaxLength
            // is when the change is from Extended to Standard message Type.
            // We have to handle this and set an ID not bigger than the Maximum
            // ID value for a Standard Message (0x7FF)
            //
            if (txtID.Text.Length > txtID.MaxLength)
            {
                uiTemp = Convert.ToUInt32(txtID.Text, 16);
                txtID.Text = (uiTemp < 0x7FF) ? string.Format("{0:X3}", uiTemp) : "7FF";
            }

            txtID_Leave(this, new EventArgs());
        }

        private void chbRemote_CheckedChanged(object sender, EventArgs e)
        {
            TextBox txtbCurrentTextBox;

            txtbCurrentTextBox = txtData0;

            // If the message is a RTR, no data is sent. The textbox for data 
            // will be turned invisible
            // 
            for (int i = 0; i < 8; i++)
            {
                txtbCurrentTextBox.Visible = !chbRemote.Checked;
                if (i < 7)
                    txtbCurrentTextBox = (TextBox)this.GetNextControl(txtbCurrentTextBox, true);
            }
        }

        private void chbFilterExt_CheckedChanged(object sender, EventArgs e)
        {
            int iMaxValue;

            iMaxValue = (chbFilterExt.Checked) ? 0x1FFFFFFF : 0x7FF;

            // We check that the maximum value for a selected filter 
            // mode is used
            //
            if (nudIdTo.Value > iMaxValue)
                nudIdTo.Value = iMaxValue;

            nudIdTo.Maximum = iMaxValue;
            if (nudIdFrom.Value > iMaxValue)
                nudIdFrom.Value = iMaxValue;

            nudIdFrom.Maximum = iMaxValue;
        }

        private void rdbTimer_CheckedChanged(object sender, EventArgs e)
        {
            if (!btnRelease.Enabled)
                return;

            // According with the kind of reading, a timer, a thread or a button will be enabled
            //
            if (rdbTimer.Checked)
            {
                // Abort Read Thread if it exists
                //
                if (m_ReadThread != null)
                {
                    m_ReadThread.Abort();
                    m_ReadThread.Join();
                    m_ReadThread = null;
                }

                // Enable Timer
                //
                tmrRead.Enabled = btnRelease.Enabled;
            }
            if (rdbEvent.Checked)
            {
                // Disable Timer
                //
                tmrRead.Enabled = false;
                // Create and start the tread to read CAN Message using SetRcvEvent()
                //
                System.Threading.ThreadStart threadDelegate = new System.Threading.ThreadStart(this.CANReadThreadFunc);
                m_ReadThread = new System.Threading.Thread(threadDelegate);
                m_ReadThread.IsBackground = true;
                m_ReadThread.Start();
            }
            if (rdbManual.Checked)
            {
                // Abort Read Thread if it exists
                //
                if (m_ReadThread != null)
                {
                    m_ReadThread.Abort();
                    m_ReadThread.Join();
                    m_ReadThread = null;
                }
                // Disable Timer
                //
                tmrRead.Enabled = false;
            }
            btnRead.Enabled = btnRelease.Enabled && rdbManual.Checked;
        }
        #endregion
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
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Vincent, Hicham, Gabriel et Louis-Normand", "Hey listen!", MessageBoxButtons.OK); // Note: À moifier
            if (result1 == DialogResult.Yes)
            {

            }
        }
        #endregion
        #endregion

        #region Actualisation des ports COM disponibles
        private void COMselector_Click(object sender, EventArgs e) // Lorsque l'user clique sur la liste des ports COM
        {
            try
            {
                COMselector.Items.Clear();                             // Flush la liste
                COMselector.Items.AddRange(SerialPort.GetPortNames()); // Relit les ports disponibles
                COMselector.SelectedIndex = 0;                         // Port par défaut = premier port
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Aucun port série ouvert");   // Enregistre le log de la connexion
            }
        }
        #endregion

        #region Connexion et déconnexion au RS232
        private void Connexion_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen == true)           // Si le port série est ouvert
            {
                if (Connexion.Text == "Déconnexion") // Si le programme est déjà connecté au port série
                {
                    try
                    {
                        Connexion.Text = "Connexion";
                        serialPort1.Close();           // Essaie de te décoonnecter
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Déconnexion du bus RS232");   // Enregistre le log de la connexion
                    }
                    catch
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Échec de la déconnexion du bus RS232");   // Enregistre le log de la connexion
                    }
                }
            }
            else // Lorsque le programme n'est pas connecté au port série et qu'un port série est disponible
            {
                try //Essaie de...
                {
                    serialPort1.BaudRate = Convert.ToInt32(BAUDselector.Text); //Le baud rate choisi dans la combobox
                    serialPort1.Parity = Parity.None;      //Aucune parité
                    serialPort1.StopBits = StopBits.One;     //1 stop bit
                    serialPort1.DataBits = 8;                //8 data bit
                    serialPort1.Handshake = Handshake.None;   //pas de handshake
                    serialPort1.PortName = COMselector.Text; //Avec le port série choisi
                    serialPort1.Open();                       //Maintenant, connecte-toi 
                    Connexion.Text = "Déconnexion";

                    timer1.Start();                           // Début du Timer du heartBeat

                    Historique.AppendText("\r\n");            // Enregistre le log de la connexion
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Connexion RS232 réussie");
                    Historique.AppendText("\r\n");
                    Historique.AppendText("à ");
                    Historique.AppendText(BAUDselector.Text);
                    Historique.AppendText(" bds");
                    Historique.AppendText(" sur ");
                    Historique.AppendText(COMselector.Text);
                }
                catch
                {
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Échec de connexion RS232 (try catch error)");   // Enregistre le log de la connexion
                }
            }
        }
        #endregion

        #region Timer de 1 seconde

            #region Envoi du HeartBeat
        private void timer1_Tick(object sender, EventArgs e) // À toutes les secondes
        {
            PingSend();
            if (serialPort1.IsOpen == true) // Envoie du heartbeat à toutes les secondes
            {
                try  // Esaie de...
                {
                    serialPort1.Write("Allo\n");    // Envoie le heartBeat
                    TXLED.BackColor = Color.Lime;   // Fait clignotter la LED de transmission
                    HeartBeatOUT.BackColor = Color.Red; // Fait clignotter la LED de transmission de HeartBeat
                    //serialPort1.DiscardInBuffer();   // Pas sur que ce soit nécessaire
                    //serialPort1.DiscardOutBuffer();  // Pas sur que ce soit nécessaire
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Heartbat Envoyé");   // Enregistre le log de la connexion (peut-être à enlever...)
                }
                catch  // Si ça ne fonctionne pas...
                {
                    HeartBeatOUT.BackColor = Color.Black;
                    label7.Text = "Failure";
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Échec de l'envoi d'un heartbeat");   // Enregistre le log de la connexion
                }
            }
        #endregion
        }

        #endregion

        #region Timer de battement du HeartBeat et de gestion de la clock
        private void timer2_Tick(object sender, EventArgs e) // Timer de remise à zero de l'affichage
        {                                                    // du heartbeat et des leds rxtx
            HeartBeatOUT.BackColor = Color.White;
            HeartBeatIN.BackColor = Color.White;
            RXLED.BackColor = Color.White;
            TXLED.BackColor = Color.White;
        }
        #endregion

        #region Lorsque le port série recoit des données

        #region Réception des données
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort1.IsOpen == true)
            {
                try
                {
                    string _charRecu = Convert.ToString(serialPort1.ReadExisting());
                    Invoke(new SetTextBoxReceiveDeleg(traitementDataReceivedUART1), new object[] { _charRecu });
                    serialPort1.DiscardInBuffer();   // Pas sur que ce soit nécessaire
                    serialPort1.DiscardOutBuffer();  // Pas sur que ce soit nécessaire
                    UART1DisplayBox.ScrollToCaret();
                }
                catch
                {
                    label7.Text = "Failure";
                   // Historique.AppendText("\r\n");
                   // Historique.AppendText("\r\n");
                   // Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                   // Historique.AppendText("\r\n");
                   // Historique.AppendText("Échec de réception des données en RS-232");   // Enregistre le log de la connexion
                }
            }
        }
        #endregion

        #region affichage par delegate

        private void traitementDataReceivedUART1(string text) // Le délégué de réception
        {
            UART1DisplayBox.AppendText(text);   // Lorsque des données sont reçues, autoscrolle l'historique
            RXLED.BackColor = Color.Red;        // Fait clignotter la led de réception

            if ((text == "Allo") || (text == "Allo\r\n"))  // Si un heartbeat est reçu
            {
                if (GhostLabel2.Text == "0")               // Si c'est le premier heartbeat
                {
                    timer2.Start();                         // Start le timer de réception des heartbeat
                    GhostLabel2.Text = "1";                 // Indique qu'un heartbeat est reçu
                }

                HeartBeatIN.BackColor = Color.Lime;       // Fait clignotter la led du heartbeat reçu
                Historique.AppendText("\r\n");
                Historique.AppendText("Heartbeat Reçu");
            }
        }
        #endregion
        #endregion

        #region Envoie manuel de données dans le port série via l'onglet de debug
        private void SendUART1_Click(object sender, EventArgs e) // Cette zone gère l'affichege de la window de debug
        {
            if (serialPort1.IsOpen == true) // Si le port série est ouvert
            {
                try
                {
                    serialPort1.Write(SendZoneUART1.Text);  // Envoie le string de texte
                    serialPort1.Write("\r\n");              // Avec un Eeter
                    SendZoneUART1.Clear();                  // Efface la zone d'envoi
                    SendZoneUART1.Focus();
                    TXLED.BackColor = Color.Lime;           // Allume la led de Tx
                }
                catch
                {
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Problème d'envoi");   // Enregistre le log de la connexion
                }
            }
            SendZoneUART1.Focus();        // Focus à la zone d'envoi
        }
        #endregion

        #region Bouton Démarrer le véhicule
        private void button3_Click(object sender, EventArgs e)
        {
          try
            {
             TxCan2(VEHICULE, DEMARRE);
             TxCan2(99, 99);

             Historique.AppendText("\r\n");
             Historique.AppendText("\r\n");
             Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
             Historique.AppendText("\r\n");
             Historique.AppendText("Séquence de démarrage du véhicule envoyée");   // Enregistre le log de la connexion
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Échec du démarrage du véhicule");   // Enregistre le log de la connexion
            } 
        }
        #endregion

        #region Bouton Arrêter le véhicule

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
             TxCan2(VEHICULE, ARRET);
             TxCan2(99, 99);

             Historique.AppendText("\r\n");
             Historique.AppendText("\r\n");
             Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
             Historique.AppendText("\r\n");
             Historique.AppendText("Séquence d'arrêt du véhicule envoyée");   // Enregistre le log de la connexio
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Échec de la séquence d'arrêt du véhicule");   // Enregistre le log de la connexion
            }
        }
        #endregion

        #region Enregistrement et effacement de l'historique
        private void sauvegarderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();        //Cré un dialog de sauvegarde
            saveFileDialog1.Filter = "Text file (*.doc)|*.doc";           //Fichier par défaut = .doc

            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK //Si l'user à appuyé sur OK
                && saveFileDialog1.FileName.Length > 0)
            {
                Historique.AppendText("\r\n"); 
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Enregistrement de l'historique sur PC");   // Enregistre le log de la connexion
                Historique.SaveFile(saveFileDialog1.FileName); //Sauvegarde le texte
            }
        }

        private void effacerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Historique.Clear();
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            UART1DisplayBox.Clear();
        }
        #endregion

        #region Autoscrolling de l'historique
        private void Historique_TextChanged(object sender, EventArgs e)
        {
            Historique.ScrollToCaret();
        }
        #endregion

        #region Gestion de la clock interne du PC
        private void timer3_Tick(object sender, EventArgs e) // À toutes les secondes...
        {
            try // Essaie de lire la clock interne du PC
            {
                PC_Clock.Text = DateTime.Now.ToString("h:mm:ss");
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur critique: Impossible de lire l'heure du PC");
            }
        }
        #endregion

        #region Bouton Synchroniser les horloges
        private void button1_Click(object sender, EventArgs e) // Lorsque l'utilisateur veut synchroniser toutes les horloges
        {
            try
            {
             TxCan2(HORLOGE, 0);
             TxCan2(99, 99);

             Historique.AppendText("\r\n");
             Historique.AppendText("\r\n");
             Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
             Historique.AppendText("\r\n");
             Historique.AppendText("Séquence de sychronisation temporelle envoyée");   // Enregistre le log de la connexion
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Échec de la séquence de sychronisation temporelle (try catch error)");   // Enregistre le log de la connexion
            }
            
        }
        #endregion

        #region Bouton de test de réception CAN de l'onglet de Debug (à utiliser avec le .HEX d'hicham SEULEMENT)
        private void button1_Click_1(object sender, EventArgs e)
        {
            ListViewItem lviCurrentItem;
            try
            {
                foreach (MessageStatus msgStatus in m_LastMsgsList)
                {
                    if (msgStatus.MarkedAsUpdated)
                    {
                        msgStatus.MarkedAsUpdated = false;
                        lviCurrentItem = lstMessages.Items[msgStatus.Position];

                        lviCurrentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
                        lviCurrentItem.SubItems[3].Text = msgStatus.DataString;
                        lviCurrentItem.SubItems[4].Text = msgStatus.Count.ToString();
                        lviCurrentItem.SubItems[5].Text = msgStatus.TimeString;
                        textBox1.Text = lviCurrentItem.SubItems[3].Text;
                    }
                }
                if (textBox1.Text == "41 42 43 44 45 46 ")
                {
                    label19.Text = "Donnée de debug reçue";

                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Donnée de debug reçue");
                }
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur de réception try catch");
            }
        }
        #endregion

        #region Lecture du CAN aux 50 ms et prise de décisions
        private void timer4RealTimeCAN_Tick(object sender, EventArgs e)  // À toutes les 50 ms
        {
            ListViewItem lviCurrentItem;

            try // Essaie de lire le bus CAN
            {
                foreach (MessageStatus msgStatus in m_LastMsgsList)
                {
                    if (msgStatus.MarkedAsUpdated)
                    { // ATTENTION: CETTE FONCTIONNALITÉ UTILISE UN GHOST LABEL
                        msgStatus.MarkedAsUpdated = false;
                        lviCurrentItem = lstMessages.Items[msgStatus.Position];
                        lviCurrentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
                        lviCurrentItem.SubItems[3].Text = msgStatus.DataString;
                        lviCurrentItem.SubItems[4].Text = msgStatus.Count.ToString();
                        lviCurrentItem.SubItems[5].Text = msgStatus.TimeString;

                        Poltergeist.Text = lviCurrentItem.SubItems[3].Text; // Met le string lu dans le GHOST LABEL master
                        PoidsLabel.Text = lviCurrentItem.SubItems[3].Text;
                        GhostLabelDeRéception.Text = Poltergeist.Text; // Met le string lu dans un GHOST LABEL
                        RangedTrame.Text = Poltergeist.Text;           // Met le string lu dans un GHOST LABEL
                        RangedTrame.Text = RangedTrame.Text.Remove(RangedTrame.Text.Length - 7); // Enlève la timestamp et la variation
                        GhostLabelDeRéception.Text = GhostLabelDeRéception.Text.Remove(GhostLabelDeRéception.Text.Length - 4); // Enlève la valeur variable et la timestamp
                        
                        string input = Poltergeist.Text;
                        string sub = input.Substring(3, 2);
                        Range.Text = sub;
                    }
                }
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur de réception try catch");
            }

            if(CANid.Text == "ListViewSubItem: {005h}")
            {
                string Dizaine = Poltergeist.Text.Remove(0, 1);
                Dizaine = Dizaine.Remove(Dizaine.Length - 12);

                string Unite = Poltergeist.Text.Remove(0, 4);
                Unite = Unite.Remove(Unite.Length - 9);

                string Dizieme = Poltergeist.Text.Remove(0, 10);
                Dizieme = Dizieme.Remove(Dizieme.Length - 3);

                string Centieme = Poltergeist.Text.Remove(0, 13);

               // int Dix = Convert.ToInt16(Dizaine);
               // int Un = Convert.ToInt16(Unite);
               // int Diz = Convert.ToInt16(Dizieme);
               // int Cnt = Convert.ToInt16(Centieme);

                //string Newton = Convert.ToString(Dix + Un + (Diz / 10) + (Cnt / 100));

                try
                {
                    LblPoidBloc.Text = Dizaine;
                    Poidunite.Text = Unite;
                    PoidDizieme.Text = Dizieme;
                    PoidCentieme.Text = Centieme;
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Tramme de poids reçue");
                    Historique.AppendText("Poids = ");
                    Historique.AppendText(LblPoidBloc.Text);

                    if (lblDirection.Text == "Horaire")
                    {
                        pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;
                    }

                    if (lblDirection.Text == "Antihoraire")
                    {
                        pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsAnti;
                    }

                    try
                    {
                        TxCan2(VEHICULE, DEMARRE);
                        TxCan2(99, 99);

                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Séquence de démarrage du véhicule envoyée suite à la mesure du poids du bloc");   // Enregistre le log de la connexion
                    }
                    catch
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Échec du démarrage du véhicule après mesure du bloc");   // Enregistre le log de la connexion 
                    }
                }
                catch
                {
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Erreur de try catch lors de la lecture du poids");
                }
            }
        }
        #endregion

        #region Directives de changement de direction pour le véhicule

        // Note: Cet événement se déclanche à l'aide de l'événement
        //       TectChanged du label lblDirection
        private void lblDirection_TextChanged(object sender, EventArgs e)
        {
            if (lblDirection.Text == "Horaire") // Si le text du label exige un sens horaire
            {
                try // essaie d'envoyer la commande CAN HORAIRE
                {
                    TPCANMsg CANMsg;
                    TPCANStatus stsResult;

                    CANMsg = new TPCANMsg();
                    CANMsg.DATA = new byte[2];
                    CANMsg.ID = 004;
                    CANMsg.LEN = 4;
                    CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

                    for (int i = 0; i < CANMsg.LEN; i++)
                    {
                        if (i == 0) { CANMsg.DATA[0] = 08; }
                        if (i == 1) { CANMsg.DATA[1] = 00; }
                        if (i == 2) { CANMsg.DATA[2] = 00; }
                        if (i == 3) { CANMsg.DATA[3] = 00; }
                        if (i == 4) { CANMsg.DATA[4] = 00; }
                        if (i == 5) { CANMsg.DATA[5] = 00; }
                        if (i == 6) { CANMsg.DATA[6] = 00; }
                        if (i == 7) { CANMsg.DATA[7] = 00; }
                    }

                    stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg);

                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Go CounterClockwise command successfully sent");   // Enregistre le log de la connexion
                    }
                    else
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Error sending Go CounterClockwise command");   // Enregistre le log de la connexion
                    }
                }
                catch
                {
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Error sending data on CAN bus (try catch error)");   // Enregistre le log de la connexion
                }
                
            }

            if (lblDirection.Text == "Antihoraire") // Si le text du label exige un sens antihoraire
            {
                try
                {
                    TPCANMsg CANMsg;
                    TPCANStatus stsResult;

                    CANMsg = new TPCANMsg();
                    CANMsg.DATA = new byte[2];
                    CANMsg.ID = 004;
                    CANMsg.LEN = 4;
                    CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

                    for (int i = 0; i < CANMsg.LEN; i++)
                    {
                        if (i == 0) { CANMsg.DATA[0] = 08; }
                        if (i == 1) { CANMsg.DATA[1] = 01; }
                        if (i == 2) { CANMsg.DATA[2] = 00; }
                        if (i == 3) { CANMsg.DATA[3] = 00; }
                        if (i == 4) { CANMsg.DATA[4] = 00; }
                        if (i == 5) { CANMsg.DATA[5] = 00; }
                        if (i == 6) { CANMsg.DATA[6] = 00; }
                        if (i == 7) { CANMsg.DATA[7] = 00; }
                    }

                    stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg);

                    if (stsResult == TPCANStatus.PCAN_ERROR_OK)
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Go CounterClockwise command successfully sent");   // Enregistre le log de la connexion
                    }

                    else
                    {
                        Historique.AppendText("\r\n");
                        Historique.AppendText("\r\n");
                        Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        Historique.AppendText("\r\n");
                        Historique.AppendText("Error sending Go CounterClockwise command");   // Enregistre le log de la connexion
                    }
                }
                catch
                {
                    Historique.AppendText("\r\n");
                    Historique.AppendText("\r\n");
                    Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Historique.AppendText("\r\n");
                    Historique.AppendText("Error sending data on CAN bus (try catch error)");   // Enregistre le log de la connexion
                }
            }
        }
        #endregion

        #region Envoyer des données sur le bus CAN
        void TxCan2(byte objet, byte cmd)
        {
            try
            {
            TPCANMsg CANMsg;
            TPCANStatus stsResult;

            CANMsg = new TPCANMsg();
            CANMsg.DATA = new byte[8];

            CANMsg.ID = 003; // 006 c'est pour faire des tests avec le fichier .HEX d'Hicham. Mettre 004 pour la version finale
            CANMsg.LEN = 7;
            CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

            for (int i = 0; i < CANMsg.LEN; i++) // Incrémenteur de longueur de message
            {
                if (i == 0) { CANMsg.DATA[0] = objet; } // Note: Ce programme en C# envoie en base 10
                if (i == 1) { CANMsg.DATA[1] = cmd; }   // 0x30 (en ascii) = caractère '0'
                if (i == 2) { CANMsg.DATA[2] = Convert.ToByte(DateTime.Now.ToString("HH")); } // 0 (en base 10) = caractère ascii '0' (0x30)
                if (i == 3) { CANMsg.DATA[3] = Convert.ToByte(DateTime.Now.ToString("mm")); }
                if (i == 4) { CANMsg.DATA[4] = Convert.ToByte(DateTime.Now.ToString("ss")); }
            }
            stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg); // Écrit le message

            if (stsResult == TPCANStatus.PCAN_ERROR_OK) // Si l'envoie est réussi
            {

            }

            else // Si l'envoi à échoué
            {

            }

            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur de Try Catch d'envoie sur le bus CAN");   // Enregistre le log de la connexion
            }
        }
        #endregion

        #region envoi de PING
        void PingSend()
        {
            try
            {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "Allo";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 120;
            string Adresse = "172.18.42.25";

            PingReply reply = pingSender.Send(Adresse, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
            {
                label20.Text = "OK";
            }
            else label20.Text = "Fail";
            }

            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur de Try Catch lors de l'envoie du Heartbeat sur TCP/IP");   // Enregistre le log de la connexion
            }
        }
        #endregion

        private void RangedTrame_TextChanged(object sender, EventArgs e)
        {
            if (RangedTrame.Text == "02") // Indice de vitesse
            {
                lblSpeed.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("La vitesse est");
                Historique.AppendText(lblSpeed.Text);
            }

            if (RangedTrame.Text == "03") // Indice de battrie
            {
                lblBattryLevel.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Batterie à ");
                Historique.AppendText(lblBattryLevel.Text);
            }
        }

        private void GhostLabelDeRéception_TextChanged(object sender, EventArgs e)
        {
            if (GhostLabelDeRéception.Text == "01 00") // Lorsque le véhicule est arrêté
            {
                lblEtatVehicule.Text = "Arrêté";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est arrêté");
            }

            if (GhostLabelDeRéception.Text == "01 01") // Lorsque le véhicule est en marche
            {
                lblEtatVehicule.Text = "En marche";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est en marche");
            }

            if (GhostLabelDeRéception.Text == "01 02") // Lorsque le véhicule est hors circuit
            {
                lblEtatVehicule.Text = "En marche";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est Hors circuit");
                pictureBox1.Image = PCANBasicExample.Properties.Resources.hm;
            }

            if (Poltergeist.Text.Contains("34 30")) // Lorsque le bloc est métallique
            {
                lblBlocColor.Text = "Métalique";
                lblDirection.Text = "Horaire";  // *** Déclanche l'événement textchanged du label lblDirection***
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est en métal");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Metal";
            }

            if (Poltergeist.Text.Contains ("34 32")) // Lorsque le bloc est noir
            {
                lblBlocColor.Text = "Noir";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est noir");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
                label32.Text = "Noir";
            }

            if (Poltergeist.Text.Contains("34 31")) // Lorsque le bloc est orange
            {
                lblBlocColor.Text = "Orange";
                lblDirection.Text = "Antihoraire";  // Déclanche l'événement textchanged du label lblDirection
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est orange");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
                label32.Text = "Orange";
            }

            if (Poltergeist.Text.Contains("37 30")) // Lorsque le véhicule est à la station de pesée
            {
                lblStation.Text = "Station de pesée";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la station de pesée");

                if (lblDirection.Text == "Horaire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;
                }

                if (lblDirection.Text == "Antihoraire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsAnti;
                }
            }

            if (Poltergeist.Text.Contains("37 31")) //Le véhicule est à la table FESTO
            {
                lblStation.Text = "Table FESTO";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la table FESTO");

                if (lblDirection.Text == "Horaire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;
                }

                if (lblDirection.Text == "Antihoraire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.festoAnti;
                }
            }

            if (GhostLabelDeRéception.Text == "07 02") // Le véhicule est à la station 3
            {
                lblStation.Text = "Station 3";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la station 3");
            }

            if (GhostLabelDeRéception.Text == "C0 00") // demande de transfert d'historique
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Transfert de l'historique du SOC vars le PC");
            }
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            try
            {
                TPCANMsg CANMsg;
                TPCANStatus stsResult;

                CANMsg = new TPCANMsg();
                CANMsg.DATA = new byte[8];

                CANMsg.ID = 003; // 006 c'est pour faire des tests avec le fichier .HEX d'Hicham. Mettre 004 pour la version finale
                CANMsg.LEN = 7;
                CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

                for (int i = 0; i < CANMsg.LEN; i++) // Incrémenteur de longueur de message
                {
                    if (i == 0) { CANMsg.DATA[0] = 43; } // Note: Ce programme en C# envoie en base 10
                    if (i == 1) { CANMsg.DATA[1] = 30; }   // 0x30 (en ascii) = caractère '0'
                    if (i == 2) { CANMsg.DATA[2] = Convert.ToByte(DateTime.Now.ToString("HH")); } // 0 (en base 10) = caractère ascii '0' (0x30)
                    if (i == 3) { CANMsg.DATA[3] = Convert.ToByte(DateTime.Now.ToString("mm")); }
                    if (i == 4) { CANMsg.DATA[4] = Convert.ToByte(DateTime.Now.ToString("ss")); }
                }
                stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg); // Écrit le message

                if (stsResult == TPCANStatus.PCAN_ERROR_OK) // Si l'envoie est réussi
                {

                }

                else // Si l'envoi à échoué
                {

                }

            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Erreur de Try Catch d'envoie sur le bus CAN");   // Enregistre le log de la connexion
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
             TxCan2(FESTO_START, FESTO);
             TxCan2(99, 99);

             Historique.AppendText("\r\n");
             Historique.AppendText("\r\n");
             Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
             Historique.AppendText("\r\n");
             Historique.AppendText("Démarrage de la séquence FESTO");
            }
            catch
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Échec du démarrage de la séquence FESTO (try catch error)");
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

        private void sauvegarderToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();        //Cré un dialog de sauvegarde
            saveFileDialog1.Filter = "Text file (*.doc)|*.doc";           //Fichier par défaut = .doc

            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK //Si l'user à appuyé sur OK
                && saveFileDialog1.FileName.Length > 0)
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Enregistrement de l'historique sur PC");   // Enregistre le log de la connexion
                Historique.SaveFile(saveFileDialog1.FileName); //Sauvegarde le texte
            }
        }

        private void effacerToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Historique.Clear();
        }

        private void àProposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Vincent, Hicham, Gabriel et Louis-Normand", "Hey listen!", MessageBoxButtons.OK); // Note: À moifier
            if (result1 == DialogResult.Yes)
            {

            }
        }
        
        private void Poltergeist_TextChanged(object sender, EventArgs e)
        {
            if (GhostLabelDeRéception.Text == "31 30") // Lorsque le véhicule est arrêté
            {
                lblEtatVehicule.Text = "Arrêté";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est arrêté");
            }

            if (GhostLabelDeRéception.Text == "31 31") // Lorsque le véhicule est en marche
            {
                lblEtatVehicule.Text = "En marche";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est en marche");
            }

            if (GhostLabelDeRéception.Text == "01 02") // Lorsque le véhicule est hors circuit
            {
                lblEtatVehicule.Text = "En marche";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est Hors circuit");
                pictureBox1.Image = PCANBasicExample.Properties.Resources.hm;
            }

            if (Poltergeist.Text == "34 30") // Lorsque le bloc est métallique
            {
                lblBlocColor.Text = "Métalique";
                lblDirection.Text = "Horaire";  // *** Déclanche l'événement textchanged du label lblDirection***
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est en métal");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Metal";
            }

            if (Poltergeist.Text == "34 32") // Lorsque le bloc est noir
            {
                lblBlocColor.Text = "Noir";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est noir");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
                label32.Text = "Noir";
            }

            if (Poltergeist.Text == "34 31") // Lorsque le bloc est orange
            {
                lblBlocColor.Text = "Orange";
                lblDirection.Text = "Antihoraire";  // Déclanche l'événement textchanged du label lblDirection
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le bloc est orange");
                pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
                label32.Text = "Orange";
            }

            if (GhostLabelDeRéception.Text == "07 00") // Lorsque le véhicule est à la station de pesée
            {
                lblStation.Text = "Station de pesée";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la station de pesée");

                if (lblDirection.Text == "Horaire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;
                }

                if (lblDirection.Text == "Antihoraire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsAnti;
                }
            }

            if (GhostLabelDeRéception.Text == "07 01") //Le véhicule est à la table FESTO
            {
                lblStation.Text = "Table FESTO";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la table FESTO");

                if (lblDirection.Text == "Horaire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;
                }

                if (lblDirection.Text == "Antihoraire")
                {
                    pictureBox1.Image = PCANBasicExample.Properties.Resources.festoAnti;
                }
            }

            if (GhostLabelDeRéception.Text == "07 02") // Le véhicule est à la station 3
            {
                lblStation.Text = "Station 3";
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Le véhicule est à la station 3");
            }

            if (GhostLabelDeRéception.Text == "C0 00") // demande de transfert d'historique
            {
                Historique.AppendText("\r\n");
                Historique.AppendText("\r\n");
                Historique.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Historique.AppendText("\r\n");
                Historique.AppendText("Transfert de l'historique du SOC vars le PC");
            }
        }
    }
}