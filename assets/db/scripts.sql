-- CLIENT
CREATE TABLE trd2022_client(
    ID int NOT NULL PRIMARY KEY AUTO_INCREMENT,
    NAME VARCHAR(60),
    EMAIL VARCHAR(230),
    PASSWORD VARCHAR(22),
    DT_REGISTER DATETIME
)

INSERT INTO trd2022_client (NAME, EMAIL, PASSWORD, DT_REGISTER) VALUES('Adolfo Pinheiro', 'adolfop@email.com', '12345', CURDATE())

-- EVENT TYPE
CREATE TABLE trd2022_event_type(  
    ID int NOT NULL PRIMARY KEY AUTO_INCREMENT,
    NAME VARCHAR(50),
    DESCRIPTION VARCHAR(255)
);

INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('BUY', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('SELL', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('INFO', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('ERROR', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('START', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('FINISH', '');
INSERT INTO trd2022_event_type (NAME, DESCRIPTION) VALUES('FORCESELL', '');

-- RECOMMENDATION TYPE
CREATE TABLE trd2022_rec_type(  
    ID int NOT NULL PRIMARY KEY AUTO_INCREMENT,
    NAME VARCHAR(50) NOT NULL,
    DESCRIPTION VARCHAR(255)
);

INSERT INTO trd2022_rec_type (NAME, DESCRIPTION) VALUES('DAY', '')
INSERT INTO trd2022_rec_type (NAME, DESCRIPTION) VALUES('HOUR', '')
INSERT INTO trd2022_rec_type (NAME, DESCRIPTION) VALUES('MINUTE', '')

-- EVENT
CREATE TABLE trd2022_event(  
    ID int NOT NULL PRIMARY KEY AUTO_INCREMENT COMMENT 'Primary Key',
    ID_CLIENT int,
    ID_EVENT_TYPE INT NOT NULL,
    ID_REC_TYPE INT,
    ASSET VARCHAR(13),
    INITIAL_PRICE DOUBLE,
    FINAL_PRICE DOUBLE,
    QUANTITY DOUBLE,
    VALORIZATION DOUBLE,
    INFO VARCHAR(255),
    MOMENT DATETIME,
    FOREIGN KEY (ID_CLIENT) REFERENCES trd2022_client(ID),
    FOREIGN KEY (ID_EVENT_TYPE) REFERENCES trd2022_event_type(ID),
    FOREIGN KEY (ID_REC_TYPE) REFERENCES trd2022_rec_type(ID)
);

-- EXECUTION
CREATE TABLE trd2022_execution(  
    ID int NOT NULL PRIMARY KEY AUTO_INCREMENT,
    ID_CLIENT INT NOT NULL,
    EXCH_KEY VARCHAR(255),
    EXCH_SECRET VARCHAR(255),
    MAX_AMOUNT INT,
    FREE_MODE BIT,
    PERC_LAST_EXEC DOUBLE,
    PERC_FROM_BEGI DOUBLE,
    VALUE_LAST_EXEC DOUBLE,
    VALUE_FROM_BEGI DOUBLE,
    AUTHORIZED BIT,
    DT_START_LAST_EXEC DATETIME,
    DT_END_LAST_EXEC DATETIME,
    DT_REGISTER DATETIME,
    FOREIGN KEY (ID_CLIENT) REFERENCES trd2022_client(ID)
);

-- STP GET EVENTS
DELIMITER $$
CREATE DEFINER=`root`@`localhost` PROCEDURE `STP_TRD2022_GET_EVENTS`()
BEGIN
	SELECT eve.ID as 'id', typ.NAME as 'name', eve.INFO as 'info', eve.ASSET as 'asset', eve.INITIAL_PRICE as 'initialPrice', eve.FINAL_PRICE as 'finalPrice', eve.QUANTITY as 'quantity', TRUNCATE(eve.VALORIZATION, 2) as 'valorization', eve.MOMENT as 'moment' FROM develop2020.trd2022_event eve INNER JOIN develop2020.trd2022_event_type typ on eve.ID_EVENT_TYPE = typ.ID 
    WHERE eve.ID >=  
    (
		select ID from develop2020.trd2022_event 
		where ID_EVENT_TYPE = 5
		ORDER BY MOMENT DESC
		LIMIT 1
	)
    ORDER BY MOMENT;
END$$
DELIMITER ;