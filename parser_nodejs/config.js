// Конфигурация парсера
export const config = {
  // WordPress REST API
  apiBaseUrl: 'https://25-12.ru/wp-json/wp/v2',
  
  // Категории программ
  categories: {
    'ПП': 'профессиональная-переподготовка',
    'ПК': 'повышение-квалификации',
    'ДОП': 'дополнительное-образование'
  },
  
  // База данных PostgreSQL
  database: {
    host: '26.242.232.93',  // Явно IPv4 вместо localhost
    port: 5432,
    user: 'postgres',
    password: '1',
    database: 'MPT2512'
  },
  
  // Настройки парсинга
  parsing: {
    perPage: 100,        // Количество курсов за запрос
    timeout: 30000,      // Таймаут запроса (мс)
    retryAttempts: 3,    // Количество попыток при ошибке
    retryDelay: 2000     // Задержка между попытками (мс)
  }
};
