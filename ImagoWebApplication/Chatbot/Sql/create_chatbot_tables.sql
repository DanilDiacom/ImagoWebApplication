-- Чат-бот сайта imagodt.cz (ImagoWebApplication/Chatbot). Скрипт можно запускать повторно.
-- Переписки старше Chatbot:RetentionDays (по умолчанию 7 дней без сообщений) сайт удаляет сам.

IF OBJECT_ID(N'[dbo].[chatbot_conversation]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[chatbot_conversation] (
        [id]               UNIQUEIDENTIFIER NOT NULL,
        [visitor_id]       UNIQUEIDENTIFIER NOT NULL,          -- cookie посетителя (imago_chat_vid)
        [date_create]      DATETIME         NOT NULL,
        [date_update]      DATETIME         NOT NULL,          -- последнее сообщение
        [is_closed]        BIT              NOT NULL CONSTRAINT [DF_chatbot_conversation_is_closed] DEFAULT (0),   -- «Новый диалог»
        [language]         NVARCHAR(10)     NULL,
        [page_url]         NVARCHAR(300)    NULL,
        [summary_ru]       NVARCHAR(1000)   NULL,              -- суть последнего вопроса (для Telegram)
        [history_summary]  NVARCHAR(MAX)    NULL,              -- резюме старых сообщений для модели
        [summarized_count] INT              NOT NULL CONSTRAINT [DF_chatbot_conversation_summarized_count] DEFAULT (0),
        [messages]         NVARCHAR(MAX)    NOT NULL,          -- JSON: [{ "role", "content", "status", "date" }]
        [user_messages]    INT              NOT NULL CONSTRAINT [DF_chatbot_conversation_user_messages] DEFAULT (0),
        [customer_name]    NVARCHAR(100)    NULL,
        [customer_contact] NVARCHAR(100)    NULL,
        [escalated_count]  INT              NOT NULL CONSTRAINT [DF_chatbot_conversation_escalated_count] DEFAULT (0),   -- сколько заявок ушло в Telegram
        [escalated_at]     INT              NOT NULL CONSTRAINT [DF_chatbot_conversation_escalated_at] DEFAULT (0),      -- число сообщений на момент последней заявки
        [date_escalated]   DATETIME         NULL,
        CONSTRAINT [PK_chatbot_conversation] PRIMARY KEY CLUSTERED ([id])
    );

    CREATE INDEX [IX_chatbot_conversation_visitor] ON [dbo].[chatbot_conversation] ([visitor_id], [date_update]);
    CREATE INDEX [IX_chatbot_conversation_update] ON [dbo].[chatbot_conversation] ([date_update]);
END
GO

-- Заявки из чата в Telegram (кнопка «Ответить клиенту»)
IF OBJECT_ID(N'[dbo].[chatbot_escalation]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[chatbot_escalation] (
        [id]               INT IDENTITY(1,1) NOT NULL,
        [conversation_id]  UNIQUEIDENTIFIER NOT NULL,
        [visitor_id]       UNIQUEIDENTIFIER NOT NULL,
        [date_create]      DATETIME         NOT NULL,
        [language]         NVARCHAR(10)     NULL,               -- язык переписки: на него переводится ответ
        [customer_name]    NVARCHAR(100)    NULL,
        [customer_contact] NVARCHAR(100)    NULL,
        [question_ru]      NVARCHAR(1000)   NULL,               -- суть вопроса по-русски
        [question_text]    NVARCHAR(MAX)    NULL,               -- вопросы клиента как есть
        [answer_original]  NVARCHAR(MAX)    NULL,               -- ответ из Telegram как написан
        [answer_sent]      NVARCHAR(MAX)    NULL,               -- что ушло клиенту (после перевода)
        [answered_by]      NVARCHAR(100)    NULL,
        [date_answered]    DATETIME         NULL,
        [knowledge_id]     INT              NULL,               -- если ответ добавлен в базу знаний
        CONSTRAINT [PK_chatbot_escalation] PRIMARY KEY CLUSTERED ([id])
    );
    CREATE INDEX [IX_chatbot_escalation_conversation] ON [dbo].[chatbot_escalation] ([conversation_id]);
END
GO

-- База знаний, пополняемая ответами из Telegram («Добавить в базу знаний»)
IF OBJECT_ID(N'[dbo].[chatbot_knowledge]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[chatbot_knowledge] (
        [id]             INT IDENTITY(1,1) NOT NULL,
        [question]       NVARCHAR(MAX)    NOT NULL,
        [answer]         NVARCHAR(MAX)    NOT NULL,
        [escalation_id]  INT              NULL,
        [created_by]     NVARCHAR(100)    NULL,
        [date_create]    DATETIME         NOT NULL,
        [is_active]      BIT              NOT NULL CONSTRAINT [DF_chatbot_knowledge_is_active] DEFAULT (1),
        CONSTRAINT [PK_chatbot_knowledge] PRIMARY KEY CLUSTERED ([id])
    );
END
GO

-- Подписчики Telegram-бота (/start, /end): получают заявки и могут отвечать клиентам
IF OBJECT_ID(N'[dbo].[chatbot_telegram_subscriber]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[chatbot_telegram_subscriber] (
        [user_id]      BIGINT          NOT NULL,
        [user_name]    NVARCHAR(100)   NULL,
        [user_lang]    NVARCHAR(10)    NULL,
        [chat_id]      BIGINT          NOT NULL,
        [date_create]  DATETIME        NOT NULL,
        CONSTRAINT [PK_chatbot_telegram_subscriber] PRIMARY KEY CLUSTERED ([user_id])
    );
END
GO

-- Обработанные обновления Telegram: после перезапуска сайта одно и то же обновление не обрабатывается дважды
IF OBJECT_ID(N'[dbo].[chatbot_telegram_update]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[chatbot_telegram_update] (
        [id]           INT             NOT NULL,                -- update_id от Telegram
        [date_create]  DATETIME        NOT NULL,
        [text]         NVARCHAR(500)   NULL,
        CONSTRAINT [PK_chatbot_telegram_update] PRIMARY KEY CLUSTERED ([id])
    );
END
GO
