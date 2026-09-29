#include <yaxi/routex-client.h>
#include "vendor/json.hpp"
#include <cstring>
#include <cstdlib>
#include <type_traits>

using json = nlohmann::json;
template <class... T> struct overloaded : T... { using T::operator()...; };
template <class... T> overloaded(T...) -> overloaded<T...>;
template<class T> json optional_json(const std::optional<T>& value) { return value ? json(*value) : json(nullptr); }
template<class T> requires std::is_enum_v<T> json optional_json(const T& value) { return json(static_cast<int>(value)); }
template<class T> std::optional<T> opt(const json& j, const char* key) { return j.contains(key) && !j[key].is_null() ? std::optional<T>(j[key].get<T>()) : std::nullopt; }

json response(const yaxi::ServiceResponse& value) {
    return std::visit(overloaded{
        [](const yaxi::ServiceResult& r) -> json { return {{"kind","result"},{"jwt",r.jwt},{"session",optional_json(r.session)},{"connectionData",optional_json(r.connectionData)}}; },
        [](const yaxi::Redirect& r) -> json { return {{"kind","redirect"},{"url",r.url},{"context",r.context}}; },
        [](const yaxi::RedirectHandle& r) -> json { return {{"kind","redirectHandle"},{"handle",r.handle},{"context",r.context}}; },
        [](const yaxi::Dialog& d) -> json {
            json j={{"kind","dialog"},{"message",optional_json(d.message)},{"dialogContext",d.context ? json(static_cast<int>(*d.context)) : json(nullptr)}};
            if(d.image) j["image"]={{"mimeType",d.image->mimeType},{"data",d.image->data},{"hhdUcData",optional_json(d.image->hhdUcData)}};
            j["input"]=std::visit(overloaded{
                [](const yaxi::Confirmation& c)->json { return {{"kind","confirmation"},{"context",c.context},{"pollingDelaySecs",optional_json(c.pollingDelaySecs)}}; },
                [](const yaxi::Field& f)->json { return {{"kind","field"},{"context",f.context},{"type",static_cast<int>(f.type)},{"secrecy",static_cast<int>(f.secrecyLevel)},{"minLength",optional_json(f.minLength)},{"maxLength",optional_json(f.maxLength)}}; },
                [](const yaxi::Selection& s)->json { json options=json::array(); for(const auto& o:s.options) options.push_back({{"key",o.key},{"label",o.label},{"explanation",optional_json(o.explanation)}}); return {{"kind","selection"},{"context",s.context},{"options",options}}; }
            },d.input); return j;
        }
    },value);
}
json error(const yaxi::Error& e) {
    static const char* names[]={"RequestError","UnexpectedError","Canceled","InvalidCredentials","ServiceBlocked","Unauthorized","AccessExceeded","PeriodOutOfBounds","UnsupportedProduct","PaymentFailed","UnexpectedValue","TicketError","ProviderError","ResponseError","NotFound","InterruptError"};
    json j={{"kind","error"},{"type",names[e.index()]}};
    std::visit([&](const auto& value){
        j["message"]=value.what();
        if constexpr(requires {value.userMessage;}) j["userMessage"]=optional_json(value.userMessage);
        if constexpr(requires {value.code;}) j["code"]=optional_json(value.code);
    },e); return j;
}
template<class T, class F> json result(yaxi::Result<T> r,F convert) {
    if(auto* e=std::get_if<yaxi::Error>(&r)) return error(*e);
    return convert(std::get<T>(std::move(r)));
}
json connection(const yaxi::ConnectionInfo& c) {
    return {{"id",c.id},{"name",c.displayName},{"countries",c.countries},{"logoId",c.logoId},{"labels",c.labels},
        {"credentials",{{"full",c.credentials.full},{"userId",c.credentials.userId},{"none",c.credentials.none}}},
        {"userIdLabel",optional_json(c.userId)},{"passwordLabel",optional_json(c.password)},{"advice",optional_json(c.advice)}};
}
json call(yaxi::RoutexClient& client,const json& j) {
    auto op=j.at("op").get<std::string>();
    if(op=="version") return {{"kind","version"},{"bridge",1},{"sdk","0.2.0"}};
    auto ticket=j.at("ticket").get<std::string>();
    if(op=="search") return result(client.search(ticket,{yaxi::TermSearchFilter{j.at("term").get<std::string>()}},true,30),[](const auto& values){json a=json::array();for(const auto& c:values)a.push_back(connection(c));return json{{"kind","connections"},{"items",a}};});
    if(op=="info") return result(client.info(ticket,j.at("connectionId")),connection);
    if(op=="registerRedirect") return result(client.registerRedirectUri(ticket,j.at("handle"),j.at("redirectUri")),[](const auto& url){return json{{"kind","url"},{"url",url}};});
    if(op=="confirm" || op=="respond") {
        auto context=j.at("context").get<std::vector<uint8_t>>();auto service=j.at("service").get<std::string>();
        bool confirm=op=="confirm";auto input=j.value("input",std::string());
        if(service=="Accounts") return result(confirm?client.confirmAccounts(ticket,context):client.respondAccounts(ticket,context,input),response);
        if(service=="Balances") return result(confirm?client.confirmBalances(ticket,context):client.respondBalances(ticket,context,input),response);
        if(service=="Transactions") return result(confirm?client.confirmTransactions(ticket,context):client.respondTransactions(ticket,context,input),response);
        if(service=="Transfer") return result(confirm?client.confirmTransfer(ticket,context):client.respondTransfer(ticket,context,input),response);
        throw std::invalid_argument("Unknown service");
    }
    const auto& c=j.at("credentials");
    yaxi::Credentials credentials{c.at("connectionId"),opt<std::string>(c,"userId"),opt<std::string>(c,"password"),opt<std::vector<uint8_t>>(c,"connectionData")};
    std::variant<yaxi::Credentials,yaxi::ConnectionData> access=credentials;
    if(j.value("useConnectionData",false)) {
        if(op!="accounts" && op!="balances" && op!="transactions") throw std::invalid_argument("Read-only access required");
        access=c.at("connectionData").get<yaxi::ConnectionData>();
    }
    auto session=opt<yaxi::Session>(j,"session");
    if(op=="accounts") return result(client.accounts(access,ticket,{yaxi::AccountField::Iban,yaxi::AccountField::Currency,yaxi::AccountField::OwnerName,yaxi::AccountField::Name,yaxi::AccountField::DisplayName},std::nullopt,session,true),response);
    if(op=="balances") {
        std::vector<yaxi::AccountReference> accounts;
        for(const auto& a:j.at("accounts")) accounts.push_back({a.at("iban"),opt<std::string>(a,"currency")});
        return result(client.balances(access,ticket,accounts,session,true),response);
    }
    if(op=="transactions") return result(client.transactions(access,ticket,session,true),response);
    if(op=="transfer") {
        const auto& t=j.at("transfer");
        yaxi::TransferDetails details{};details.amount=t.at("amount");details.currency=t.at("currency");details.creditorIban=t.at("iban");details.creditorName=t.at("name");details.remittance=opt<std::string>(t,"purpose");
        details.endToEndIdentification=opt<std::string>(t,"endToEndId");
        std::optional<std::chrono::time_point<std::chrono::system_clock>> execution;
        if(j.contains("executionUnix")&&!j["executionUnix"].is_null())execution=std::chrono::system_clock::time_point(std::chrono::seconds(j["executionUnix"].get<int64_t>()));
        yaxi::AccountReference debtor{j.at("debtorIban"),opt<std::string>(j,"debtorCurrency")};
        return result(client.transfer(credentials,ticket,yaxi::PaymentProduct::DefaultSepaCreditTransfer,{details},debtor,std::nullopt,execution,session),response);
    }
    throw std::invalid_argument("Unknown operation");
}
extern "C" {
__declspec(dllexport) void* sb_yaxi_create(const char* url) noexcept { try { return new yaxi::RoutexClient(url && *url ? std::optional<std::string>(url) : std::nullopt); } catch(...) { return nullptr; } }
__declspec(dllexport) void sb_yaxi_destroy(void* handle) noexcept { delete static_cast<yaxi::RoutexClient*>(handle); }
__declspec(dllexport) void sb_yaxi_free(char* memory) noexcept { if(memory){ volatile char* p=memory;auto n=std::strlen(memory);while(n--)*p++=0;std::free(memory);} }
__declspec(dllexport) char* sb_yaxi_call(void* handle,const char* request) noexcept {
    try {
        json out;
        try { if(!handle||!request)throw std::invalid_argument("Invalid arguments");out=call(*static_cast<yaxi::RoutexClient*>(handle),json::parse(request)); }
        catch(const std::exception&){out={{"kind","error"},{"type","BridgeError"},{"message","Native request failed"}};}
        catch(...){out={{"kind","error"},{"type","BridgeError"},{"message","Unknown native failure"}};}
        auto text=out.dump();auto* p=static_cast<char*>(std::malloc(text.size()+1));if(p)std::memcpy(p,text.c_str(),text.size()+1);return p;
    } catch(...) { return nullptr; }
}
}
