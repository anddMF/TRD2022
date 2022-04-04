# TRD2022 📈
### Robot that operates cryptocurrencies on Binance. Reccomends, buys and sells assets according to the code validations and engines.
<hr>

### 🚧 Next features:
- [x] File to recover open positions
- [x] Engines switches in appsettings.json
- [ ] Queue communication (SQS and Kafka)

<hr>

### ❓ How it works
TRD2022 has three main engines:
- Recommendation
- Buy
- Sell

<b>Recommendation =></b> uses three types of calculation, that can be turned off in appsettings.json, with candlesticks combined with a moving average validation. It can recommend N number of assets and it's up to the buy engine to decide which one will enter and balance the three types of recommendations.
