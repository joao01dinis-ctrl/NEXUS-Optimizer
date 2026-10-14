# NEXUS 0.8 — interface e análise assistida

Revisão local separada da versão 0.7 submetida à Store. Não substitui nem cancela essa submissão.

## Utilização

O meu PC tem duas ações: **Analisar e otimizar**, que observa o PC e aplica apenas diferenças elegíveis, e **Só analisar**, que não muda definições. Jogos e FiveM, Rato e precisão e Histórico são acessos principais. Rede, limpeza, testes e outros ajustes ficam em Mais ferramentas e continuam pesquisáveis.

O automático usa cinco amostras espaçadas. Só pode reduzir animações de conteúdo, menus, dicas, listas de escolha e minimização se CPU ou RAM permanecerem acima dos limites das regras. Não promete libertar CPU/RAM, melhorar FPS ou corrigir problemas do jogo. Um PC sem pressão pode legitimamente terminar com zero alterações.

Antes de escrever, guarda o valor original em SQLite, confere que nada mudou, aplica e lê de novo. Uma falha no lote tenta repor as alterações desse lote. Dados incompletos ou uma análise com mais de cinco minutos não podem autorizar alterações.

Em Preferências, o automático periódico é desligado em cada arranque. Quando ativado, analisa de 15 em 15 minutos enquanto a NEXUS está aberta, pausa com sessões/Game Mode, limpezas ou testes e não reaplica ajustes desfeitos ou alterados fora da NEXUS. O restauro global pausa este automático. Nunca faz chamadas OpenAI em segundo plano.

## PrecisionFix: funções observadas e correspondência

Fonte consultada em 14/10/2026: https://www.precisionfix.xyz/ . O site descreve funções, mas não documenta algoritmos, APIs ou eficácia. Código e apresentação da NEXUS são próprios.

| Função anunciada | NEXUS 0.8 | Limite verificável |
| --- | --- | --- |
| Tracking / Sensi / Flick | Aceleração do ponteiro com undo e cálculo de sensibilidade ao mudar de DPI | Não altera sensor ou mira nem afeta necessariamente Raw Input. O cálculo pressupõe a mesma escala linear. |
| Boost FiveM | Biblioteca, sessões reversíveis, guia e comparações de capturas | Sem injeção, edição do jogo, manipulação de anti-cheat ou ganhos fabricados. |
| Otimização automática | Observações CPU/RAM, política local restrita e modo periódico opcional | Só efeitos visuais condicionados aos dados. |
| Economia de recursos | Modo leve a cada dez segundos | Reduz as atualizações da própria NEXUS; não purga RAM de outras apps. |
| Tema claro/escuro | Preferência local da interface | Guardada sem segredos. |
| Iniciar com Windows | Atalho para o gestor de arranque do Windows | Não se inscreve automaticamente no arranque. |
| Idiomas | Português de Portugal | Outros idiomas não implementados nesta revisão. |
| Proteção em tempo real / níveis / verificar ficheiros | Acesso à Segurança do Windows e explicação do âmbito | Não é antivírus, não bloqueia processos por adivinhação e não implementa scanner próprio. |

Não há paridade integral demonstrada com PrecisionFix/TL. Alegações comerciais não são evidência de melhoria de input lag ou FPS.

## OpenAI opcional

Responses API, saída estruturada, `store:false`, um pedido por análise manual, timeout de 25 segundos e sem repetição automática. A resposta nunca é executada como comando. Só pode recomendar efeitos visuais já aprovados pelas regras locais.

Só envia percentagens CPU/RAM, quantidade de RAM, alimentação, número de volumes com pouco espaço e chaves dos efeitos permitidos. Não envia inventário de hardware, nomes de processos, caminhos, IPs, CSV, logs ou base de dados. O utilizador ativa o envio nas Preferências. A política OpenAI continua a aplicar-se: `store:false` não significa ausência de todos os registos do fornecedor. A API pode ter custos.

Não se inclui chave do programador no executável ou num pacote público. O cliente lê `OPENAI_API_KEY` do ambiente de processo. A configuração local de desenvolvimento é ignorada pelo Git e fica fora da publicação.

Não há chave configurada nesta revisão. Nenhuma chamada real foi feita e a ligação OpenAI continua por validar. Os testes HTTP usam um transporte simulado em memória e tokens fictícios.

Referências: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [Responses](https://developers.openai.com/api/docs/guides/migrate-to-responses), [GPT-4.1 mini](https://developers.openai.com/api/docs/models/gpt-4.1-mini), [SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa).
