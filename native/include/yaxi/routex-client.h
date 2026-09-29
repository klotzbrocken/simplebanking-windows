#pragma once

#include <chrono>
#include <cstdint>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <variant>
#include <vector>

#if defined(_WIN32)
#if defined(YAXI_BUILDING_DLL)
#define YAXI_API __declspec(dllexport)
#else
#define YAXI_API __declspec(dllimport)
#endif
#elif defined(__GNUC__) || defined(__clang__)
#define YAXI_API __attribute__((visibility("default")))
#else
#define YAXI_API
#endif

#ifdef _MSC_VER
#pragma warning(push)
#pragma warning(disable : 4251) // member needs dll-interface
#pragma warning(disable : 4275) // non dll-interface base class (std::runtime_error)
#endif

namespace yaxi {
using TraceId = std::vector<uint8_t>;
using ConnectionId = std::string;
using ConnectionData = std::vector<uint8_t>;
using Session = std::vector<uint8_t>;
using InputContext = std::vector<uint8_t>;
using ConfirmationContext = std::vector<uint8_t>;

class YAXI_API RequestError : public std::runtime_error {
  public:
    RequestError(std::string error)
        : std::runtime_error("Request error"), error(std::move(error)) {}

    std::string error;
};

class YAXI_API UnexpectedError : public std::runtime_error {
  public:
    UnexpectedError(std::optional<std::string> userMessage)
        : std::runtime_error("Unexpected service error"), userMessage(std::move(userMessage)) {}

    std::optional<std::string> userMessage;
};

class YAXI_API Canceled : public std::runtime_error {
  public:
    Canceled() : std::runtime_error("Canceled") {}
};

class YAXI_API InvalidCredentials : public std::runtime_error {
  public:
    InvalidCredentials(std::optional<std::string> userMessage)
        : std::runtime_error("Invalid credentials"), userMessage(std::move(userMessage)) {}

    std::optional<std::string> userMessage;
};

class YAXI_API ServiceBlocked : public std::runtime_error {
  public:
    enum class Code : uint8_t {
        /**
         * Something is not set up for the user, e.g., there are no TAN methods.
         */
        MissingSetup,
        /**
         * User attention is required via another channel.
         * Typically the user needs to log into the Online Banking.
         */
        ActionRequired,
    };

    ServiceBlocked(std::optional<std::string> userMessage, std::optional<Code> code)
        : std::runtime_error("Service blocked"), userMessage(std::move(userMessage)), code(code) {}

    std::optional<std::string> userMessage;
    std::optional<Code> code;
};

class YAXI_API Unauthorized : public std::runtime_error {
  public:
    Unauthorized(std::optional<std::string> userMessage)
        : std::runtime_error("Unauthorized"), userMessage(std::move(userMessage)) {}

    std::optional<std::string> userMessage;
};

class YAXI_API AccessExceeded : public std::runtime_error {
  public:
    AccessExceeded(std::optional<std::string> userMessage)
        : std::runtime_error("Access exceeded"), userMessage(std::move(userMessage)) {}

    std::optional<std::string> userMessage;
};

class YAXI_API PeriodOutOfBounds : public std::runtime_error {
  public:
    PeriodOutOfBounds(std::optional<std::string> userMessage)
        : std::runtime_error("Period out of bounds"), userMessage(std::move(userMessage)) {}

    std::optional<std::string> userMessage;
};

class YAXI_API UnsupportedProduct : public std::runtime_error {
  public:
    enum class Reason : uint8_t {
        /**
         * The amount is not allowed for the payment product.
         */
        Limit,

        /**
         * The recipient is not capable to receive the payment product.
         */
        Recipient,
    };

    UnsupportedProduct(std::optional<std::string> userMessage, std::optional<Reason> reason)
        : std::runtime_error("Unsupported product"), userMessage(std::move(userMessage)),
          reason(reason) {}

    std::optional<std::string> userMessage;
    std::optional<Reason> reason;
};

class YAXI_API PaymentFailed : public std::runtime_error {
  public:
    enum class Code : uint8_t {
        LimitExceeded,
        InsufficientFunds,
    };

    PaymentFailed(std::optional<std::string> userMessage, std::optional<Code> code)
        : std::runtime_error("Payment canceled or rejected"), userMessage(std::move(userMessage)),
          code(code) {}

    std::optional<std::string> userMessage;
    std::optional<Code> code;
};

class YAXI_API UnexpectedValue : public std::runtime_error {
  public:
    UnexpectedValue(std::string error)
        : std::runtime_error("Unexpected value"), error(std::move(error)) {}

    std::string error;
};

class YAXI_API TicketError : public std::runtime_error {
  public:
    enum class Code : uint8_t {
        Missing,
        Invalid,
        MissingKey,
        UnknownKey,
        Mismatch,
        Expired,
        InvalidLifetime,
        ExpiredKey,
        KeyEnvironmentMismatch,
    };

    TicketError(std::string error, Code code)
        : std::runtime_error(error), error(std::move(error)), code(code) {}

    std::string error;
    Code code;
};

class YAXI_API ProviderError : public std::runtime_error {
  public:
    enum class Code : uint8_t {
        Maintenance,
    };

    ProviderError(std::optional<std::string> userMessage, std::optional<Code> code)
        : std::runtime_error("The account-servicing provider indicated an error"),
          userMessage(std::move(userMessage)), code(code) {}

    std::optional<std::string> userMessage;
    std::optional<Code> code;
};

class YAXI_API ResponseError : public std::runtime_error {
  public:
    ResponseError(std::string response)
        : std::runtime_error("Error response"), response(std::move(response)) {}

    std::string response;
};

class YAXI_API NotFound : public std::runtime_error {
  public:
    NotFound() : std::runtime_error("Resource not found") {}
};

/**
 * The service call requires user interaction to complete.
 *
 * Only requests passing `ConnectionData` instead of `Credentials` produce this
 * error; with `Credentials` a `Dialog` or `Redirect` gets returned instead.
 * Recover by running the user through a request with `Credentials` and
 * persisting the `connectionData` it returns.
 */
class YAXI_API InterruptError : public std::runtime_error {
  public:
    InterruptError()
        : std::runtime_error("The transaction is not possible without user interaction") {}
};

using Error = std::variant<RequestError, UnexpectedError, Canceled, InvalidCredentials,
                           ServiceBlocked, Unauthorized, AccessExceeded, PeriodOutOfBounds,
                           UnsupportedProduct, PaymentFailed, UnexpectedValue, TicketError,
                           ProviderError, ResponseError, NotFound, InterruptError>;

template <typename T> using Result = std::variant<T, Error>;

/**
 * Data returned by YAXI Open Banking services, authenticated with an HMAC.
 *
 * jwt can be used for transfer to a remote system as JSON Web Token https://jwt.io/.
 * The remote system can verify and read the data from the "data" claim.
 * To read the data locally, the frontend can decode the JWT without verification.
 *
 * Besides the value itself, it contains a timestamp and a ticket identifier
 * (bound to known input parameters and service type).
 */
struct ServiceResult {
    std::string jwt;
    std::optional<Session> session;
    std::optional<ConnectionData> connectionData;
};

/**
 * Context of a user dialog.
 */
enum class DialogContext : uint8_t {
    /**
     * SCA or TAN process.
     *
     * There are multiple cases, distinguishable by the input:
     * - `Confirmation`: Decoupled process (e.g. confirmation in a SCA app).
     * - `Selection`: TAN method selection.
     * - `Field`: TAN entry.
     */
    Sca,

    /**
     * Account selection.
     *
     * A `Selection` gets returned with this context when an account has to be selected.
     * Note that there might be just a single option that may be chosen automatically without user
     * interaction.
     */
    Accounts,

    /**
     * Pending redirect confirmation.
     *
     * A `Confirmation` gets returned with this context when a redirect got confirmed but no result
     * is known yet.
     */
    Redirect,

    /**
     * Pending SCT Inst payment.
     *
     * A `Confirmation` gets returned with this context when an SCT Inst payment has been
     * initialized and not reached the final status yet.
     */
    PaymentStatus,

    /**
     * Verification of Payee confirmation.
     *
     * A `Confirmation` gets returned with this context when an explicit confirmation of the
     * creditor is required due to a name mismatch. Note that this confirmation has legal
     * implications, releasing the bank from liabilities in case of the transfer to an unintended
     * receiver due to incorrect creditor data.
     */
    VopConfirmation,

    /**
     * Pending Verification of Payee check.
     * A `Confirmation` gets returned with this context when a Verification of Payee check is still
     * pending.
     */
    VopCheck,
};

/**
 * Image data for a dialog.
 */
struct Image {
    std::string mimeType;

    /**
     * Binary data in the format defined by mimeType.
     */
    std::vector<uint8_t> data;

    /**
     * HHD_UC data block
     *
     * In cases where the ASPSP provides HHD_UC data for optical coupling with a HandHeld-Device
     * for the generation of an OTP, especially for an HHD_OPT animated graphic, the raw HHD_UC
     * data stream is provided here.
     *
     * The publicly available document "HandHeld-Device (HHD) for the generation of an OTP HHD
     * enhancement for optical interfaces" describes how to implement the animated graphic for
     * HHD_OPT in section C. data provides a pre-rendered animated GIF
     * to be presented with a width of 62.5 mm.
     */
    std::optional<std::vector<uint8_t>> hhdUcData;
};

/**
 * Just a primary action to confirm the dialog.
 */
struct Confirmation {
    /**
     * Context object that can be used to confirm the dialog.
     */
    ConfirmationContext context;

    /**
     * If polling is acceptable, a delay in seconds is specified for which the client has to wait
     * before automatically confirming.
     */
    std::optional<uint32_t> pollingDelaySecs;
};

/**
 * A dialog option.
 */
struct DialogOption {
    std::string key;
    std::string label;
    std::optional<std::string> explanation;
};

/**
 * A selection of options the user can choose from.
 */
struct Selection {
    /**
     * Options are meant to be rendered e.g. as radio buttons where the user must select exactly
     * one to for a confirmation button to get enabled. Another example for an implementation is
     * one button per option that immediately confirms the selection.
     */
    std::vector<DialogOption> options;

    /**
     * Context object that can be used to respond to the dialog.
     */
    InputContext context;
};

/**
 * Type of an input field.
 */
enum class InputType : uint8_t {
    Date,
    Email,
    Number,
    Phone,
    Text,
};

/**
 * Level of secrecy for an input field.
 */
enum class SecrecyLevel : uint8_t {
    /**
     * The data is not a secret.
     */
    Plain,

    /**
     * The data is a one-time password. This can usually be treated as
     * no secret but the implementer might still choose to mask the input.
     */
    Otp,

    /**
     * The data is a secret password. Input must be masked.
     */
    Password,
};

/**
 * An input field.
 */
struct Field {
    /**
     * Type that may be used for showing hints or dedicated keyboard layouts and for applying input
     * restrictions or validation.
     */
    InputType type;

    /**
     * Indicates if the input should be masked.
     */
    SecrecyLevel secrecyLevel;

    /**
     * Minimal length to allow.
     */
    std::optional<uint32_t> minLength;

    /**
     * Maximum length to allow.
     */
    std::optional<uint32_t> maxLength;

    /**
     * Context object that can be used to respond to the dialog.
     */
    InputContext context;
};

/**
 * User dialog.
 *
 * This is meant to be displayed as a dialog in some User Interface and consists of:
 *
 * - A way to cancel the dialog (typically an X symbol and / or a "Cancel" button).
 * - The display part:
 *   - The message.
 *   - An optional image.
 * - The interactive part defined by the input.
 *
 * The input contains a context for continuing the
 * process at the service that issued the dialog object.
 */
struct Dialog {
    std::optional<DialogContext> context;
    std::optional<std::string> message;
    std::optional<Image> image;
    /**
     * Data defining the interactive part of a user dialog.
     */
    std::variant<Confirmation, Selection, Field> input;
};

/**
 * User redirect.
 *
 * The user is meant to get sent to the url and the context can
 * be used for continuing the process at the service that issued the redirect object afterward.
 *
 * A web application needs to direct the user agent to the returned URL.
 * A desktop or mobile application could either open it in a browser or inside an element like a
 * WebView.
 */
struct Redirect {
    std::string url;
    ConfirmationContext context;
};

/**
 * Incomplete user redirect.
 *
 * A final redirect URI needs to get registered, using the handle, to receive the URL to send the
 * user to.
 */
struct RedirectHandle {
    std::string handle;
    ConfirmationContext context;
};

using ServiceResponse = std::variant<ServiceResult, Dialog, Redirect, RedirectHandle>;

/**
 * Requirements for user identifier and password.
 */
struct CredentialsModel {
    /**
     * A full set of credentials may be provided to support fully embedded authentication (including
     * scraped redirects).
     */
    bool full;

    /**
     * Only a user identifier without a password may be provided.
     * This is typically the case for decoupled authentication where the user e.g. authorizes access
     * in a mobile application. Note that if password-less authentication fails (e.g. as no device
     * for decoupled authentication is set up for the user and a redirect is not supported), an
     * error is returned and the transaction has to get restarted with a full set of credentials.
     */
    bool userId;

    /**
     * Credentials are not required. The user will provide them to the service provider during a
     * redirect.
     */
    bool none;
};

/**
 * Connection meta data
 */
struct ConnectionInfo {
    /**
     * Unique identifier.
     */
    ConnectionId id;

    /**
     * ISO 3166-1 ALPHA-2 country codes.
     */
    std::vector<std::string> countries;

    /**
     * Display name.
     */
    std::string displayName;

    /**
     * Credentials model.
     */
    CredentialsModel credentials;

    /**
     * Human-friendly label for the user identifier if relevant.
     */
    std::optional<std::string> userId;

    /**
     * Human-friendly label for the PIN / password if relevant.
     */
    std::optional<std::string> password;

    /**
     * Advice for the credentials to be displayed.
     */
    std::optional<std::string> advice;

    /**
     * Logo identifier.
     */
    std::string logoId;

    /**
     * ISO 20022 BICFIIdentifiers.
     *
     * Note that this is only included in search results if requested.
     */
    std::optional<std::vector<std::string>> bics;

    /**
     * National bank codes (as used in IBANs).
     *
     * Note that this is only included in search results if requested.
     */
    std::optional<std::vector<std::string>> bankCodes;

    /**
     * Labels categorizing the connection (e.g. "beta", "fints").
     */
    std::vector<std::string> labels;
};

/**
 * List of ISO 3166-1 alpha-2 country codes to consider.
 */
struct CountriesSearchFilter {
    std::vector<std::string> countries;
};

/**
 * String filter for the provider / product name or any alias.
 */
struct NameSearchFilter {
    std::string name;
};

/**
 * String filter for the BIC.
 */
struct BicSearchFilter {
    std::string bic;
};

/**
 * String filter for the (national) bank code.
 */
struct BankCodeSearchFilter {
    std::string bankCode;
};

/**
 * String filter for any of those fields.
 */
struct TermSearchFilter {
    std::string term;
};

/**
 * Filters for the connection lookup
 *
 * String filters look for the given value anywhere in the related field, case-insensitive.
 */
using SearchFilter = std::variant<CountriesSearchFilter, NameSearchFilter, BicSearchFilter,
                                  BankCodeSearchFilter, TermSearchFilter>;

/**
 * Details to contain in search results.
 */
enum class Details : uint8_t {
    Bics,
    BankCodes,
};

struct Credentials {
    ConnectionId connectionId;
    std::optional<std::string> userId;
    std::optional<std::string> password;
    std::optional<ConnectionData> connectionData;
};

enum class AccountStatus : uint8_t {
    Available,
    Terminated,
    Blocked,
};

enum class AccountType : uint8_t {
    /**
     * Account used to post debits and credits.
     * ISO 20022 ExternalCashAccountType1Code CACC.
     */
    Current,
    /**
     * Account used for credit card payments.
     * ISO 20022 ExternalCashAccountType1Code CARD.
     */
    Card,
    /**
     * Account used for savings.
     * ISO 20022 ExternalCashAccountType1Code SVGS.
     */
    Savings,
    /**
     * Account used for call money.
     * No dedicated ISO 20022 code (falls into SVGS).
     */
    CallMoney,
    /**
     * Account used for time deposits.
     * No dedicated ISO 20022 code (falls into SVGS).
     */
    TimeDeposit,
    /**
     * Account used for loans.
     * ISO 20022 ExternalCashAccountType1Code LOAN.
     */
    Loan,
    Securities,
    Insurance,
    Commerce,
    Rewards,
};

enum class SupportedService : uint8_t {
    CollectPayment,
};

class YAXI_API AccountFilter {
  public:
    struct Inner;
    std::unique_ptr<Inner> inner;

    explicit AccountFilter(Inner inner);
    ~AccountFilter();

    AccountFilter(const AccountFilter &) = delete;
    AccountFilter &operator=(const AccountFilter &) = delete;

    AccountFilter(AccountFilter &&) noexcept;
    AccountFilter &operator=(AccountFilter &&) noexcept;

    AccountFilter operator&&(AccountFilter &&filter) &&;
    AccountFilter operator||(AccountFilter &&filter) &&;

    static AccountFilter supports(const SupportedService &service);
};

class YAXI_API AccountField {
  private:
    uint8_t value;
    AccountField(const uint8_t &value) : value(value) {}
    class OptionalStringField;
    class CurrencyField;
    class StatusField;
    class TypeField;

  public:
    static const OptionalStringField Iban;
    static const OptionalStringField Number;
    static const OptionalStringField Bic;
    static const OptionalStringField BankCode;
    static const CurrencyField Currency;
    static const OptionalStringField Name;
    static const OptionalStringField DisplayName;
    static const OptionalStringField OwnerName;
    static const OptionalStringField ProductName;
    static const StatusField Status;
    static const TypeField Type;

    constexpr bool operator==(const AccountField &other) const { return value == other.value; }
    constexpr operator uint8_t() const { return value; }
};

class YAXI_API AccountField::OptionalStringField : public AccountField {
  public:
    AccountFilter operator==(const std::optional<std::string> &val) const;
    AccountFilter operator!=(const std::optional<std::string> &val) const;
};

class YAXI_API AccountField::CurrencyField : public AccountField {
  public:
    AccountFilter operator==(const std::string &val) const;
    AccountFilter operator!=(const std::string &val) const;
};

class YAXI_API AccountField::StatusField : public AccountField {
  public:
    AccountFilter operator==(const std::optional<AccountStatus> &val) const;
    AccountFilter operator!=(const std::optional<AccountStatus> &val) const;
};

class YAXI_API AccountField::TypeField : public AccountField {
  public:
    AccountFilter operator==(const std::optional<AccountType> &val) const;
    AccountFilter operator!=(const std::optional<AccountType> &val) const;
};

struct AccountReference {
    std::string iban;
    std::optional<std::string> currency;
};

enum class PaymentProduct : uint8_t {
    /**
     * SEPA Credit Transfer (SCT) in EUR
     */
    SepaCreditTransfer,

    /**
     * SEPA Instant Credit Transfer (SCT Inst) in EUR
     */
    SepaInstantCreditTransfer,

    /**
     * Default SEPA Credit Transfer in EUR
     *
     * Tries SCT Inst with a fallback to SCT if this is supported.
     * Otherwise, SCT is used.
     */
    DefaultSepaCreditTransfer,

    /**
     * International credit transfer outside of SEPA (typically SWIFT)
     */
    CrossBorderCreditTransfer,

    /**
     * Domestic credit transfer in the domestic, non-EUR currency
     */
    DomesticCreditTransfer,

    /**
     * Instant domestic credit transfer in the domestic, non-EUR currency
     */
    DomesticInstantCreditTransfer,
};

struct CreditorAddress {
    std::string townName;
    /**
     * ISO 3166-1 ALPHA-2 country code.
     */
    std::string country;
};

enum class ChargeBearer : uint8_t {
    BorneByDebtor,
    BorneByCreditor,
    Shared,
    FollowingServiceLevel,
};

struct TransferDetails {
    std::optional<std::string> endToEndIdentification;
    /**
     * Decimal amount, e.g. "123.45".
     */
    std::string amount;
    /**
     * ISO 4217 Alpha 3 currency code.
     */
    std::string currency;
    std::string creditorIban;
    std::optional<std::string> creditorAgentBic;
    std::string creditorName;
    std::optional<CreditorAddress> creditorAddress;
    std::optional<std::string> remittance;
    std::optional<ChargeBearer> chargeBearer;
};

class [[nodiscard]] YAXI_API RoutexClient final {
  public:
    /**
     * Create a new client, optionally providing a custom URL.
     */
    explicit RoutexClient(const std::optional<std::string> &url = std::nullopt);

    ~RoutexClient();

    RoutexClient(const RoutexClient &) = delete;
    RoutexClient &operator=(const RoutexClient &) = delete;

    RoutexClient(RoutexClient &&) noexcept;
    RoutexClient &operator=(RoutexClient &&) noexcept;

    [[nodiscard]] std::optional<std::string> systemVersion(const std::string &ticketId) const;

    /**
     * Trace identifier returned with the last request.
     */
    [[nodiscard]] std::optional<TraceId> traceId() const;

    /**
     * Retrieve trace data.
     */
    [[nodiscard]] Result<std::string> trace(const std::string &ticket,
                                            const TraceId &traceId) const;

    /**
     * Set a redirect URI for subsequent service requests.
     *
     * Redirects will eventually forward to that URI.
     * It can be used to redirect back to a web application or to jump
     * back into the context of a desktop or mobile application.
     * If no redirect URI is set, `RedirectHandle`s will get returned instead of `Redirect`s.
     */
    void setRedirectUri(const std::string &redirectUri);

    void setRecurringConsents(bool enabled);

    /**
     * Register a redirect URI for a given redirect handle.
     *
     * Returns the URL that the user is meant to get sent to.
     */
    [[nodiscard]] Result<std::string> registerRedirectUri(const std::string &ticket,
                                                          const std::string &handle,
                                                          const std::string &redirectUri) const;

    /**
     * Search for service connections (banks and other providers).
     *
     * The result is a list of connections that match all the `SearchFilter`s.
     * If IBAN detection is enabled and the first value of a term filter is detected
     * to be a possible prefix of an IBAN that contains a national bank code,
     * the result might contain additional connections that match that bank code.
     */
    [[nodiscard]] Result<std::vector<ConnectionInfo>>
    search(const std::string &ticket, const std::vector<SearchFilter> &filters,
           bool ibanDetection = false, const std::optional<size_t> &limit = std::nullopt,
           const std::vector<Details> &details = std::vector<Details>()) const;

    /**
     * Get information for a service connection.
     */
    [[nodiscard]] Result<ConnectionInfo> info(const std::string &ticket,
                                              ConnectionId connectionId) const;

    /**
     * [Accounts service](https://docs.yaxi.tech/accounts.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    accounts(const std::variant<Credentials, ConnectionData> &access, const std::string &ticket,
             const std::vector<AccountField> &fields,
             const std::optional<AccountFilter> &filter = std::nullopt,
             const std::optional<Session> &session = std::nullopt,
             const std::optional<bool> &recurringConsents = std::nullopt) const;

    /**
     * Respond to `Dialog` with `InputContext` returned from [Accounts
     * service](https://docs.yaxi.tech/accounts.html).
     */
    [[nodiscard]] Result<ServiceResponse> respondAccounts(const std::string &ticket,
                                                          const InputContext &inputContext,
                                                          const std::string &response) const;

    /**
     * Confirm `Dialog` or `Redirect` with `ConfirmationContext` returned from [Accounts
     * service](https://docs.yaxi.tech/accounts.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    confirmAccounts(const std::string &ticket,
                    const ConfirmationContext &confirmationContext) const;

    /**
     * [Balances service](https://docs.yaxi.tech/balances.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    balances(const std::variant<Credentials, ConnectionData> &access, const std::string &ticket,
             const std::vector<AccountReference> &accounts,
             const std::optional<Session> &session = std::nullopt,
             const std::optional<bool> &recurringConsents = std::nullopt) const;

    /**
     * Respond to `Dialog` with `InputContext` returned from [Balances
     * service](https://docs.yaxi.tech/balances.html).
     */
    [[nodiscard]] Result<ServiceResponse> respondBalances(const std::string &ticket,
                                                          const InputContext &inputContext,
                                                          const std::string &response) const;

    /**
     * Confirm `Dialog` or `Redirect` with `ConfirmationContext` returned from [Balances
     * service](https://docs.yaxi.tech/balances.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    confirmBalances(const std::string &ticket,
                    const ConfirmationContext &confirmationContext) const;

    /**
     * [Transactions service](https://docs.yaxi.tech/transactions.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    transactions(const std::variant<Credentials, ConnectionData> &access, const std::string &ticket,
                 const std::optional<Session> &session = std::nullopt,
                 const std::optional<bool> &recurringConsents = std::nullopt) const;

    /**
     * Respond to `Dialog` with `InputContext` returned from [Transactions
     * service](https://docs.yaxi.tech/transactions.html).
     */
    [[nodiscard]] Result<ServiceResponse> respondTransactions(const std::string &ticket,
                                                              const InputContext &inputContext,
                                                              const std::string &response) const;

    /**
     * Confirm `Dialog` or `Redirect` with `ConfirmationContext` returned from [Transactions
     * service](https://docs.yaxi.tech/transactions.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    confirmTransactions(const std::string &ticket,
                        const ConfirmationContext &confirmationContext) const;

    /**
     * [Collect Payment service](https://docs.yaxi.tech/collect-payment.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    collectPayment(const Credentials &credentials, const std::string &ticket,
                   const std::optional<AccountReference> &account = std::nullopt,
                   const std::optional<Session> &session = std::nullopt,
                   const std::optional<bool> &recurringConsents = std::nullopt) const;

    /**
     * Respond to `Dialog` with `InputContext` returned from [Collect Payment
     * service](https://docs.yaxi.tech/collect-payment.html).
     */
    [[nodiscard]] Result<ServiceResponse> respondCollectPayment(const std::string &ticket,
                                                                const InputContext &inputContext,
                                                                const std::string &response) const;

    /**
     * Confirm `Dialog` or `Redirect` with `ConfirmationContext` returned from [Collect Payment
     * service](https://docs.yaxi.tech/collect-payment.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    confirmCollectPayment(const std::string &ticket,
                          const ConfirmationContext &confirmationContext) const;

    /**
     * [Transfer service](https://docs.yaxi.tech/transfer.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    transfer(const Credentials &credentials, const std::string &ticket,
             const PaymentProduct &product, const std::vector<TransferDetails> &details,
             const std::optional<AccountReference> &debtorAccount = std::nullopt,
             const std::optional<std::string> &debtorName = std::nullopt,
             const std::optional<std::chrono::time_point<std::chrono::system_clock>>
                 &requestedExecutionDate = std::nullopt,
             const std::optional<Session> &session = std::nullopt,
             const std::optional<bool> &recurringConsents = std::nullopt) const;

    /**
     * Respond to `Dialog` with `InputContext` returned from [Transfer
     * service](https://docs.yaxi.tech/transfer.html).
     */
    [[nodiscard]] Result<ServiceResponse> respondTransfer(const std::string &ticket,
                                                          const InputContext &inputContext,
                                                          const std::string &response) const;

    /**
     * Confirm `Dialog` or `Redirect` with `ConfirmationContext` returned from [Transfer
     * service](https://docs.yaxi.tech/transfer.html).
     */
    [[nodiscard]] Result<ServiceResponse>
    confirmTransfer(const std::string &ticket,
                    const ConfirmationContext &confirmationContext) const;

  private:
    struct Inner;
    std::unique_ptr<Inner> inner;
};
} // namespace yaxi

#ifdef _MSC_VER
#pragma warning(pop)
#endif
